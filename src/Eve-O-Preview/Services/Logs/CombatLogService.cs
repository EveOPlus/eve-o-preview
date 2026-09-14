#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Services.Implementation;
using EveOPreview.UI;
using Microsoft.Data.Sqlite;
using Serilog;

namespace EveOPreview.Services.Logs;

/// <summary>Single background consumer; OS callbacks only mark paths dirty and wake it.</summary>
public sealed partial class CombatLogService : IWorkspaceCombatLogs, IDisposable
{
    private readonly ApplicationPreferences _preferences;
    private readonly CharacterIdentityCache? _characters;
    private readonly ILogger _logger;
    private readonly string _databasePath;
    private readonly EveLogCatalog _catalog;
    private readonly Func<DateTimeOffset> _now;
    private readonly CancellationTokenSource _stop = new();
    private readonly Channel<bool> _wake = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });
    private readonly ConcurrentDictionary<string, byte> _dirty = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<(string Action, CombatResetScope Scope, string? Character, TaskCompletionSource<CommandResult> Result)> _commands = new();
    private readonly Dictionary<string, int> _failures = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly Dictionary<string, (string Path, DateTimeOffset Session, long Length)> _localTails = new(StringComparer.OrdinalIgnoreCase);
    private long _lastLocalProbe;
    private readonly Timer _clock;
    private CombatLogSnapshot _snapshot;
    private CombatLogSnapshot _lastRealSnapshot;
    private CombatLogSettings _settings = new();
    private string _directory = "", _watchSignature = "", _status = "Disabled";
    private int _rescan = 1, _configurationDirty = 1, _watcherInterrupted;
    private int _staticDataDirty;
    private volatile bool _running = true, _disposed;
    private long _fileReadCount, _notificationCount, _reconciliationCount;
    public (long Reads, long Notifications, long Reconciliations) Diagnostics =>
        (Interlocked.Read(ref _fileReadCount), Interlocked.Read(ref _notificationCount), Interlocked.Read(ref _reconciliationCount));
    private long _lastPublish, _lastPrune;
    private const string AccessWarning = "A log cannot be read. It will retry on its next change; use Retry after checking access.";
    private string? _warning;
    private readonly Task _worker;
    public Task Completion => _worker;
    public event Action? LogsChanged;
    public event Action<CombatOverlayEvent>? CombatEvent;
    public event Func<CombatSimulation, Task<CommandResult>>? SimulationRequested;
    public CharacterSystemCache CurrentSystems { get; }

    public Task<CommandResult> SimulateLogEventAsync(CombatSimulation simulation)
    {
        if (_disposed || simulation is null || (!simulation.Stop && !simulation.AllVisibleThumbnails && simulation.FullTitle?.StartsWith("EVE - ", StringComparison.Ordinal) != true)
            || simulation.DurationSeconds is < 0 or > 60
            || !double.IsFinite(simulation.Amount) || simulation.Amount is < 0 or > 1_000_000_000
            || !Enum.IsDefined(simulation.Direction) || !Enum.IsDefined(simulation.DamageType)
            || !Enum.IsDefined(simulation.Platform) || !Enum.IsDefined(simulation.Effect)
            || simulation.Kind.HasValue && !Enum.IsDefined(simulation.Kind.Value)
            || !double.IsFinite(simulation.DamageScale) || simulation.DamageScale is <= 0 or > 100)
            return Task.FromResult(CommandResult.Error("Choose a character and valid simulation values."));
        return SimulationRequested?.Invoke(simulation) ?? Task.FromResult(CommandResult.Error("Start previews to simulate an event on a thumbnail."));
    }

    public CombatLogService(ApplicationPreferences preferences, CharacterIdentityCache characters, ILogger logger, CharacterSystemCache currentSystems,
        StaticData.StaticDataService staticData)
        : this(preferences, characters, logger, Path.Combine(Path.GetDirectoryName(preferences.FilePath)!, "Logs", "Combat.sqlite"),
            EveLogCatalog.Load(staticData), () => DateTimeOffset.UtcNow, currentSystems) { }

    public CombatLogService(ApplicationPreferences preferences, CharacterIdentityCache? characters, ILogger logger,
        string databasePath, EveLogCatalog catalog, Func<DateTimeOffset> now, CharacterSystemCache? currentSystems = null)
    {
        _preferences = preferences; _characters = characters; _logger = logger; _databasePath = databasePath;
        _catalog = catalog; _now = now;
        CurrentSystems = currentSystems ?? new();
        _snapshot = new("Starting", "", now(), preferences.CombatLogs.WindowSeconds, [], []);
        _lastRealSnapshot = _snapshot;
        _clock = new Timer(_ => Wake(), null, Timeout.Infinite, Timeout.Infinite);
        preferences.Changed += ConfigurationChanged;
        if (characters is not null) characters.Changed += Wake;
        if (catalog.StaticData is not null) catalog.StaticData.DataReplaced += StaticDataChanged;
        // Startup may have upgraded an installed index without changing its FC build.
        // Re-enrich the retained visible window once; never re-ingest or dispatch hits.
        _staticDataDirty = 1;
        _worker = Task.Run(Run);
        Wake();
    }

    public CombatLogSettings ReadLogSettings() => _preferences.CombatLogs;
    public CombatLogSnapshot ReadLogs() => Volatile.Read(ref _snapshot);
    public Task<CommandResult> SaveLogSettingsAsync(CombatLogSettings settings)
    {
        if (_disposed) return Task.FromResult(CommandResult.Error("Log service has stopped."));
        try { _preferences.SetCombatLogs(settings); return Task.FromResult(CommandResult.Ok("Log settings saved for all profiles")); }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        { return Task.FromResult(CommandResult.Error(ex is ArgumentException ? ex.Message : "Could not save log settings.")); }
    }
    public Task<CommandResult> ResetCombatAsync() => ResetCombatAsync(CombatResetScope.All);
    public Task<CommandResult> ResetCombatAsync(CombatResetScope scope, string? character = null) => Enqueue("reset", scope, character);
    public Task<CommandResult> RescanLogsAsync() => Enqueue("rescan");
    private Task<CommandResult> Enqueue(string action, CombatResetScope scope = CombatResetScope.All, string? character = null)
    {
        if (_disposed) return Task.FromResult(CommandResult.Error("Log service has stopped."));
        var completion = new TaskCompletionSource<CommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _commands.Enqueue((action, scope, character, completion)); Wake(); return completion.Task;
    }
    public void Start() { _running = true; ConfigurationChanged(); }
    public void Stop() { _running = false; ConfigurationChanged(); }
    private void ConfigurationChanged() { Interlocked.Exchange(ref _configurationDirty, 1); Wake(); }
    private void StaticDataChanged() { Interlocked.Exchange(ref _staticDataDirty, 1); Wake(); }
    private void Wake() { if (!_disposed) _wake.Writer.TryWrite(true); }

    private void Dirty(string path)
    {
        if (_disposed) return;
        if (_dirty.ContainsKey(path)) { Wake(); return; }
        if (_dirty.Count >= 8192) Interlocked.Exchange(ref _rescan, 1);
        else _dirty.TryAdd(path, 0);
        Wake();
    }

    private async Task Run()
    {
        CombatLogStore? store = null;
        try
        {
            while (await _wake.Reader.WaitToReadAsync(_stop.Token).ConfigureAwait(false))
            {
                while (_wake.Reader.TryRead(out _)) { }
                try
                {
                    if (Interlocked.Exchange(ref _configurationDirty, 0) != 0) Configure();
                    // Disabled first-run workspaces have no database or worker I/O.
                    // Existing history remains readable; explicit commands can create it.
                    if (store is null && !_settings.Enabled && !File.Exists(_databasePath) && _commands.IsEmpty && _simulationCommands.IsEmpty && _simulationStore is null)
                    {
                        Volatile.Write(ref _snapshot, new(_status, _directory, _now(), _settings.WindowSeconds, [], [])); Notify();
                        continue;
                    }
                    if (store is null && (_settings.Enabled || File.Exists(_databasePath) || !_commands.IsEmpty)) store = new CombatLogStore(_databasePath, _now());
                    ProcessSimulationCommands(store);
                    if (Interlocked.Exchange(ref _staticDataDirty, 0) != 0 && store is not null)
                    {
                        var players = new HashSet<string>(_characters?.KnownCharacters.Keys ?? [], StringComparer.OrdinalIgnoreCase);
                        store.RefreshDamageMetadata(_now(), _settings.WindowSeconds, entry => EveLogParser.Parse(
                            $"[ {entry.Timestamp.UtcDateTime:yyyy.MM.dd HH:mm:ss} ] ({entry.Category}) {entry.Text}", new(entry.Character), false, _catalog, players) ?? entry);
                        // Temporary simulation events already carry their chosen damage
                        // composition; never reinterpret them as NPC weapon guesses.
                    }
                    while (_commands.TryDequeue(out var command))
                    {
                        try
                        {
                            if (command.Action == "reset")
                            {
                                var resetAt = _now();
                                store!.Reset(resetAt, command.Scope, command.Character); _simulationStore?.Reset(resetAt, command.Scope, command.Character);
                                if (command.Scope is CombatResetScope.All or CombatResetScope.Damage)
                                {
                                    if (command.Character is null) { _realIndicators.Clear(); _simulationIndicators?.Clear(); }
                                    else { _realIndicators.Remove(command.Character); _simulationIndicators?.Remove(command.Character); }
                                }
                            }
                            else { Configure(); _warning = null; _failures.Clear(); _watchSignature = ""; Interlocked.Exchange(ref _rescan, 1); }
                            PublishCurrent(store);
                            command.Result.TrySetResult(CommandResult.Ok(command.Action == "reset" ? "Selected statistics reset" : "Log reconciliation requested"));
                        }
                        catch { command.Result.TrySetResult(CommandResult.Error("Could not update the combat database.")); throw; }
                    }
                    if (_settings.Enabled && _running)
                    {
                        if (Interlocked.Exchange(ref _rescan, 0) != 0)
                        {
                            if (Interlocked.Exchange(ref _watcherInterrupted, 0) != 0) _watchSignature = "";
                            EnsureWatchers();
                            Reconcile(store!);
                        }
                        ProbeLocalTails();
                        foreach (string path in _dirty.Keys.Take(64))
                        {
                            if (_stop.IsCancellationRequested) break;
                            _dirty.TryRemove(path, out _);
                            ReadFile(store!, path);
                        }
                        if (!_dirty.IsEmpty) Wake();
                    }
                    else _dirty.Clear();
                    if (Stopwatch.GetElapsedTime(_lastPrune).TotalSeconds >= 60)
                    { store?.Prune(_now(), _settings.RetentionDays); _lastPrune = Stopwatch.GetTimestamp(); }
                    AdvanceSimulation();
                    // Bound UI work under a burst. The timer also wakes the small
                    // Local-tail fallback; it never enumerates log directories.
                    if (_dirty.IsEmpty || Stopwatch.GetElapsedTime(_lastPublish).TotalMilliseconds >= 100) PublishCurrent(store);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException or System.Text.Json.JsonException)
                {
                    // A failed real read/commit must not strand temporary statistics
                    // after the one-shot display wake has fired.
                    if (IsSimulating) { EndSimulation(); _clock.Change(Timeout.Infinite, Timeout.Infinite); }
                    _logger.Warning("Combat log storage or directory access failed ({ErrorType})", ex.GetType().Name);
                    _warning = "Log storage unavailable. Check the folder and available disk space, then Retry.";
                    Volatile.Write(ref _snapshot, _snapshot with { Status = _warning }); Notify();
                    while (_commands.TryDequeue(out var command)) command.Result.TrySetResult(CommandResult.Error(_warning));
                }
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        finally
        {
            _clock.Dispose(); CloseWatchers(); store?.Dispose();
            _simulationStore?.Dispose();
            while (_simulationCommands.TryDequeue(out var simulation)) simulation.Result.TrySetResult(CommandResult.Error("Log service has stopped."));
            while (_commands.TryDequeue(out var command)) command.Result.TrySetResult(CommandResult.Error("Log service has stopped."));
        }
    }

    private void Configure()
    {
        var next = _preferences.CombatLogs;
        string directory = EveLogDirectory.Resolve(next.Directory);
        bool restart = directory != _directory || next.Enabled != _settings.Enabled || !_running;
        _directory = directory; _settings = next;
        if (restart) { CloseWatchers(); _dirty.Clear(); _localTails.Clear(); _failures.Clear(); _warning = null; }
        _status = !_running ? "Stopped" : !next.Enabled ? "Disabled - enable log reading to begin" : "Watching EVE logs";
        if (next.Enabled && _running && (restart || _watchers.Count == 0)) Interlocked.Exchange(ref _rescan, 1);
    }

    private void EnsureWatchers()
    {
        string parent = Path.GetDirectoryName(_directory) ?? _directory;
        while (!Directory.Exists(parent) && Path.GetDirectoryName(parent) is { } ancestor) parent = ancestor;
        string[] directories = [parent, _directory, Path.Combine(_directory, "Gamelogs"), Path.Combine(_directory, "Chatlogs")];
        string signature = string.Join('|', directories.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase));
        if (_watchSignature == signature) return;
        CloseWatchers();
        foreach (string directory in directories.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            bool files = directory.Equals(Path.Combine(_directory, "Gamelogs"), StringComparison.OrdinalIgnoreCase)
                || directory.Equals(Path.Combine(_directory, "Chatlogs"), StringComparison.OrdinalIgnoreCase);
            var watcher = new FileSystemWatcher(directory)
            {
                Filter = files ? "*.txt" : "*", IncludeSubdirectories = false, InternalBufferSize = 32 * 1024,
                NotifyFilter = files ? NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite : NotifyFilters.DirectoryName
            };
            void OnChange(object sender, FileSystemEventArgs args)
            { Interlocked.Increment(ref _notificationCount); if (files) Dirty(args.FullPath); else { Interlocked.Exchange(ref _rescan, 1); Wake(); } }
            watcher.Changed += OnChange; watcher.Created += OnChange; watcher.Deleted += OnChange;
            watcher.Renamed += (_, args) => { if (files) { Dirty(args.OldFullPath); Dirty(args.FullPath); }
                else { Interlocked.Exchange(ref _rescan, 1); Wake(); } };
            watcher.Error += (_, _) =>
            {
                Interlocked.Exchange(ref _watcherInterrupted, 1);
                Interlocked.Exchange(ref _rescan, 1); Wake();
            };
            _watchers.Add(watcher);
            watcher.EnableRaisingEvents = true; // Subscribe before the startup/recovery enumeration.
        }
        _watchSignature = signature;
    }

    private bool IsCandidate(string path)
    {
        string? parent = Path.GetDirectoryName(path);
        return Path.GetExtension(path).Equals(".txt", StringComparison.OrdinalIgnoreCase)
            && (string.Equals(parent, Path.Combine(_directory, "Gamelogs"), StringComparison.OrdinalIgnoreCase)
                || string.Equals(parent, Path.Combine(_directory, "Chatlogs"), StringComparison.OrdinalIgnoreCase)
                    && Path.GetFileName(path).StartsWith("Local_", StringComparison.OrdinalIgnoreCase));
    }

    private void Reconcile(CombatLogStore store)
    {
        Interlocked.Increment(ref _reconciliationCount);
        if (!Directory.Exists(_directory)) { _status = "EVE logs folder missing - waiting for it to be created"; return; }
        _status = "Watching EVE logs";
        var known = new HashSet<string>(store.KnownPaths(), StringComparer.OrdinalIgnoreCase);
        foreach (string folder in new[] { "Gamelogs", "Chatlogs" })
        {
            string path = Path.Combine(_directory, folder);
            if (!Directory.Exists(path)) { _status = "Waiting for Gamelogs and Chatlogs folders"; continue; }
            // Metadata enumeration only at startup, reconfiguration or watcher recovery.
            foreach (var file in new DirectoryInfo(path).EnumerateFiles(folder == "Chatlogs" ? "Local_*.txt" : "*.txt"))
            {
                if (_stop.IsCancellationRequested || !_running) return;
                if (!known.Contains(file.FullName) && file.LastWriteTimeUtc < _now().UtcDateTime.AddDays(-_settings.RetentionDays)) continue;
                // Consume reconciliation sequentially instead of filling the bounded
                // notification queue. Large directories cannot starve later files by
                // repeatedly enqueueing the same first 8192 paths on overflow.
                _dirty.TryRemove(file.FullName, out _);
                ReadFile(store, file.FullName);
                if (Stopwatch.GetElapsedTime(_lastPublish).TotalMilliseconds >= 250) PublishCurrent(store);
            }
        }
    }

    private void ReadFile(CombatLogStore store, string path)
    {
        if (!IsCandidate(path)) return;
        try
        {
            Interlocked.Increment(ref _fileReadCount);
            StoredLogFile? previous;
            LogReadBatch batch;
            long length;
            using (var stream = CompleteLogReader.OpenShared(path))
            {
                string identity = CompleteLogReader.Identity(stream);
                previous = store.ReadFile(identity);
                length = stream.Length;
                batch = CompleteLogReader.Read(stream, identity, previous?.Cursor);
            }
            var header = previous is not null && previous.Cursor.Generation == batch.Cursor.Generation ? previous.Header : new LogHeader();
            foreach (var line in batch.Lines) header = EveLogParser.ReadHeader(header, line.Text);
            var players = new HashSet<string>(_characters?.KnownCharacters.Keys ?? [], StringComparer.OrdinalIgnoreCase);
            var entries = new List<PositionedLogEntry>();
            bool chat = Path.GetDirectoryName(path)!.Equals(Path.Combine(_directory, "Chatlogs"), StringComparison.OrdinalIgnoreCase);
            foreach (var line in batch.Lines)
            {
                ParsedLogEntry? entry = EveLogParser.Parse(line.Text, header, chat, _catalog, players);
                // Never allow a bad clock or corrupted future entry to pin live DPS/location.
                if (entry is not null && entry.Timestamp <= _now().AddSeconds(2)) entries.Add(new(line.Offset, entry));
            }
            store.Commit(new(path, batch.Cursor, header), entries);
            _simulationStore?.Commit(new(path, batch.Cursor, header), entries);
            if (chat && !header.Ambiguous && header.Channel == "Local" && header.Listener is { } listener)
            {
                var session = header.Session ?? entries.Select(x => x.Entry.Timestamp).DefaultIfEmpty(DateTimeOffset.MinValue).Max();
                if (!_localTails.TryGetValue(listener, out var tail) || session >= tail.Session || tail.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
                    _localTails[listener] = (path, session > tail.Session ? session : tail.Session, length);
            }
            if (!header.Ambiguous)
                // EVE timestamps have one-second precision and writes can be delayed.
                // Recent newly committed hits trigger a full receipt-time indicator;
                // startup history still cannot replay an old fight as a live alert.
                foreach (var entry in entries.Where(x => x.Entry.Direction.HasValue && x.Entry.Timestamp >= _now().AddSeconds(-30)))
                    if (previous is null || previous.Cursor.Generation != batch.Cursor.Generation || entry.Offset >= previous.Cursor.Offset)
                        AcceptCombatEntry(entry.Entry, simulated: false);
            _failures.Remove(path);
            if (_warning == AccessWarning && !_failures.Any(x => x.Value > 4)) _warning = null;
            if (batch.OversizedLines > 0) _warning = "Some malformed or oversized log lines were skipped.";
            if (header.Ambiguous) _warning = "A log contains conflicting listeners and was excluded from character statistics.";
            if (batch.More) Dirty(path);
        }
        catch (FileNotFoundException) { _failures.Remove(path); }
        catch (DirectoryNotFoundException) { Interlocked.Exchange(ref _rescan, 1); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Do not wait on, acquire, or break the client's locks. Retry asynchronously
            // after this specific failure; ordinary ingestion never polls files.
            int attempt = _failures.GetValueOrDefault(path) + 1;
            _failures[path] = attempt;
            if (attempt <= 4) _ = Retry(path, new[] { 50, 250, 1000, 5000 }[attempt - 1]);
            else _warning = AccessWarning;
        }
        catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
        { _warning = "A log contains unsupported formatting; its position was retained for Retry."; }
    }

    private async Task Retry(string path, int milliseconds)
    {
        try { await Task.Delay(milliseconds, _stop.Token).ConfigureAwait(false); Dirty(path); }
        catch (OperationCanceledException) { }
    }

    private void ProbeLocalTails()
    {
        if (Stopwatch.GetElapsedTime(_lastLocalProbe).TotalMilliseconds < 250) return;
        _lastLocalProbe = Stopwatch.GetTimestamp();
        // Cached writes can be readable before Windows emits Size/LastWrite.
        // Query an actual shared handle, not cached directory metadata. Only the
        // newest known Local session per listener needs this fallback; old files
        // and discovery still use notifications. Remember the checked byte length,
        // not the complete-line offset, so an unchanged partial line is not reread.
        foreach (var pair in _localTails.ToArray())
        {
            var tail = pair.Value;
            if (_failures.ContainsKey(tail.Path)) continue; // Preserve bounded retries.
            try
            {
                using var stream = CompleteLogReader.OpenShared(tail.Path);
                if (stream.Length != tail.Length) Dirty(tail.Path); // Growth or truncation.
            }
            catch (FileNotFoundException) { _localTails.Remove(pair.Key); }
            catch (DirectoryNotFoundException) { _localTails.Remove(pair.Key); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Dirty(tail.Path); }
        }
    }

    private void Publish(CombatLogStore store)
    {
        var identities = _characters?.KnownCharacters.ToDictionary(x => x.Key, x => x.Value.CharacterId, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, long?>(StringComparer.OrdinalIgnoreCase);
        var snapshot = store.Snapshot(_now(), _settings.WindowSeconds, _warning ?? _status, _directory, identities);
        var indicators = ReferenceEquals(store, _simulationStore) ? _simulationIndicators : _realIndicators;
        if (indicators is not null)
        {
            foreach (string name in indicators.Where(x => x.Value.Any < _now().AddSeconds(-10)).Select(x => x.Key).ToArray()) indicators.Remove(name);
            snapshot = snapshot with { Characters = snapshot.Characters.Select(character =>
            {
                var pulse = indicators.GetValueOrDefault(character.Name);
                return character with { LastIncomingDamageAt = pulse.Any, LastPlayerDamageAt = pulse.Player,
                    IncomingDamageStartedAt = pulse.AnyStarted, PlayerDamageStartedAt = pulse.PlayerStarted };
            }).ToArray() };
        }
        if (!ReferenceEquals(store, _simulationStore)) _lastRealSnapshot = snapshot;
        foreach (var character in snapshot.Characters)
            if (character.SolarSystem is not null && character.LocationObservedAt is { } observed)
                CurrentSystems.Observe(character.Name, character.SolarSystem, observed);
        Volatile.Write(ref _snapshot, snapshot); _lastPublish = Stopwatch.GetTimestamp(); Notify();
        bool live = _settings.Enabled && _running && (snapshot.Characters.Any(x => x.Categories.Any(c => c.Dps.Incoming > 0 || c.Dps.Outgoing > 0))
            || snapshot.Characters.Any(x => x.RepairRates.Any(r => r.PerSecond.Incoming > 0 || r.PerSecond.Outgoing > 0))
            || snapshot.RecentEntries.Any(x => x.Direction.HasValue && x.Timestamp > _now()));
        bool flashing = indicators?.Values.Any(x => x.Any is { } time && time.AddSeconds(_settings.FlashSeconds) > _now()) == true;
        bool localTails = _settings.Enabled && _running && _localTails.Count > 0;
        _clock.Change(live || flashing || IsSimulating || localTails ? 250 : Timeout.Infinite, Timeout.Infinite);
    }
    private void Notify()
    {
        if (_disposed) return;
        try { LogsChanged?.Invoke(); }
        catch (Exception ex) { _logger.Warning("Log presentation subscriber failed ({ErrorType})", ex.GetType().Name); }
    }
    private void NotifyCombat(CombatOverlayEvent entry)
    {
        if (_disposed) return;
        try { CombatEvent?.Invoke(entry); }
        catch (Exception ex) { _logger.Warning("Combat presentation subscriber failed ({ErrorType})", ex.GetType().Name); }
    }
    private void CloseWatchers()
    {
        foreach (var watcher in _watchers) watcher.Dispose();
        _watchers.Clear(); _watchSignature = "";
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _preferences.Changed -= ConfigurationChanged;
        if (_characters is not null) _characters.Changed -= Wake;
        if (_catalog.StaticData is not null) _catalog.StaticData.DataReplaced -= StaticDataChanged;
        _stop.Cancel(); _wake.Writer.TryComplete();
        // The worker owns DB/handles and closes them in finally. Never join it on
        // the Windows shutdown/UI path; uncommitted work replays from durable cursors.
    }
}
