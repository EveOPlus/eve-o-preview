#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EveOPreview.Configuration.Implementation;
using EveOPreview.UI;
using Serilog;

namespace EveOPreview.Services.StaticData;

/// <summary>Immutable SDE generations with an atomic active manifest. Import never locks
/// the active reader. Encountered names use a bounded cache; the small simulation
/// catalog is loaded lazily on a separate connection.</summary>
public sealed class StaticDataService : IWorkspaceStaticData, IDisposable
{
    private sealed record Manifest(string File, int Build, int Datasets, long Records);
    private readonly string _root;
    private readonly ILogger _logger;
    private readonly HttpClient _http;
    private readonly object _gate = new();
    private readonly Dictionary<string, StaticCombatItem?> _items = new(StringComparer.OrdinalIgnoreCase);
    private StaticDataDatabase? _database;
    private StaticDataStatus _status = new("Static data has not been downloaded.");
    private CancellationTokenSource? _download;
    private Task<CommandResult>? _update;
    private bool _disposed;
    public event Action? StaticDataChanged;
    public event Action? DataReplaced;
    public int? Build { get { lock (_gate) return _database?.Build; } }
    public int CachedItems { get { lock (_gate) return _items.Count; } }

    public StaticDataService(ApplicationPreferences preferences, ILogger logger)
        : this(Path.Combine(Path.GetDirectoryName(preferences.FilePath)!, "StaticData"), logger)
    {
        // The workspace asks before the first download. Constructing services,
        // loading an old profile or enabling logs must never imply consent.
    }
    public StaticDataService(string root, ILogger logger, HttpMessageHandler? handler = null)
    {
        _root = Path.GetFullPath(root); _logger = logger;
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = Timeout.InfiniteTimeSpan;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("EVE-O-Preview/10.0 static-data");
        try
        {
            string path = Path.Combine(_root, "current.json");
            if (File.Exists(path))
            {
                var manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(path)) ?? throw new InvalidDataException();
                if (Path.GetFileName(manifest.File) != manifest.File || !manifest.File.StartsWith("sde-", StringComparison.Ordinal)
                    || !manifest.File.EndsWith(".sqlite", StringComparison.Ordinal)) throw new InvalidDataException();
                _database = new(Path.Combine(_root, manifest.File));
                if (_database.Build != manifest.Build) throw new InvalidDataException();
                _status = new("Static data ready offline.", manifest.Build, StoredBytes: new FileInfo(Path.Combine(_root, manifest.File)).Length,
                    Datasets: manifest.Datasets, Records: manifest.Records);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or Microsoft.Data.Sqlite.SqliteException)
        { _database?.Dispose(); _database = null; _status = new("Saved static data is unavailable. Download it again."); }
    }
    public StaticDataStatus ReadStaticData() { lock (_gate) return _status; }
    public StaticCombatItem? FindItem(string name)
    {
        lock (_gate)
        {
            if (_disposed || _database is null) return null;
            if (_items.TryGetValue(name, out var item)) return item;
            try { item = _database.FindItem(name); }
            catch (Microsoft.Data.Sqlite.SqliteException) { return null; }
            if (_items.Count >= 4096) _items.Clear();
            _items[name] = item; return item;
        }
    }
    /// <summary>Caller owns the returned document. All datasets remain available for future features.</summary>
    public JsonDocument? ReadRecord(string dataset, string key)
    { lock (_gate) return _disposed ? null : _database?.ReadRecord(dataset, key); }
    public long? FindSystem(string name)
    {
        lock (_gate)
        {
            if (_disposed || _database is null) return null;
            try { return _database.FindSystem(name); }
            catch (Microsoft.Data.Sqlite.SqliteException) { return null; }
        }
    }
    private Task<CombatSimulationCatalog>? _simulationCatalog;
    private readonly CancellationTokenSource _catalogStop = new();
    public Task<CombatSimulationCatalog> ReadSimulationCatalogAsync()
    {
        lock (_gate)
        {
            if (_disposed || _database is null)
                return Task.FromResult(CombatSimulationCatalog.Unavailable("Download FC static data in Data setup."));
            if (_simulationCatalog is not null) return _simulationCatalog;
            string path = _database.FilePath;
            var cancellation = _catalogStop.Token;
            return _simulationCatalog = Task.Run(() =>
            {
                try
                {
                    using var reader = new StaticDataDatabase(path);
                    return reader.ReadSimulationCatalog(cancellation);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException
                    or JsonException or OperationCanceledException or InvalidOperationException)
                { return CombatSimulationCatalog.Unavailable("Could not load simulation choices. Update FC static data in Data setup."); }
            });
        }
    }
    public Task<CommandResult> UpdateStaticDataAsync()
    {
        lock (_gate)
        {
            if (_disposed) return Task.FromResult(CommandResult.Error("Static data service has stopped."));
            if (_update is { IsCompleted: false }) return _update;
            _download?.Dispose(); _download = new CancellationTokenSource(TimeSpan.FromMinutes(30));
            _status = _status with { Busy = true, Message = "Checking FC static data…", MessageTemplate = null, MessageArguments = null, Progress = null };
            _update = Task.Run(() => Update(_download.Token)); return _update;
        }
    }
    public void CancelStaticDataUpdate() { lock (_gate) _download?.Cancel(); }
    private void Status(string message, double? progress = null)
    {
        lock (_gate) _status = _status with { Message = message, MessageTemplate = null, MessageArguments = null, Progress = progress };
        Notify();
    }
    private void StatusFormat(FormattableString message, double? progress = null)
    {
        lock (_gate) _status = _status with { Message = message.ToString(System.Globalization.CultureInfo.InvariantCulture),
            MessageTemplate = message.Format, MessageArguments = message.GetArguments(), Progress = progress };
        Notify();
    }
    private async Task<CommandResult> Update(CancellationToken cancellation)
    {
        string? zip = null, staging = null, installed = null, pointer = null;
        try
        {
            Notify(); Directory.CreateDirectory(_root);
            // A second application must not import/delete another process's staging
            // generation. This lock belongs only to our own static-data directory.
            using var owner = new FileStream(Path.Combine(_root, "update.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            foreach (string pattern in new[] { "download-*.zip.part", "import-*.sqlite.part", "import-*.sqlite.part-journal", "current-*.json.part" })
                foreach (string abandoned in Directory.EnumerateFiles(_root, pattern)) Delete(abandoned);
            using var metadata = await _http.GetAsync("https://developers.eveonline.com/static-data/tranquility/latest.jsonl", HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(false);
            metadata.EnsureSuccessStatusCode();
            using var metaStream = await metadata.Content.ReadAsStreamAsync(cancellation).ConfigureAwait(false);
            using var metaReader = new StreamReader(metaStream);
            int build = 0, metaLength = 0;
            while (await metaReader.ReadLineAsync(cancellation).ConfigureAwait(false) is { } line)
            {
                if ((metaLength += line.Length) > 65536) throw new InvalidDataException("Invalid static metadata.");
                if (string.IsNullOrWhiteSpace(line)) continue;
                using var json = JsonDocument.Parse(line);
                if (json.RootElement.GetProperty("_key").GetString() == "sde") build = json.RootElement.GetProperty("buildNumber").GetInt32();
            }
            if (build <= 0) throw new InvalidDataException("Missing static build number.");
            if (Build == build) { Status("Static data is up to date."); return CommandResult.Ok("Static data is up to date."); }
            string token = Guid.NewGuid().ToString("N");
            zip = Path.Combine(_root, "download-" + token + ".zip.part");
            staging = Path.Combine(_root, "import-" + token + ".sqlite.part");
            Status("Downloading FC static data…", 0);
            using (var response = await _http.GetAsync($"https://developers.eveonline.com/static-data/tranquility/eve-online-static-data-{build}-jsonl.zip", HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode(); long? length = response.Content.Headers.ContentLength;
                if (length > 2L * 1024 * 1024 * 1024) throw new InvalidDataException("Static download is too large.");
                await using var input = await response.Content.ReadAsStreamAsync(cancellation).ConfigureAwait(false);
                await using var output = new FileStream(zip, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, true);
                byte[] buffer = new byte[131072]; long received = 0, reported = 0; int count;
                while ((count = await input.ReadAsync(buffer, cancellation).ConfigureAwait(false)) > 0)
                {
                    received += count; if (received > 2L * 1024 * 1024 * 1024) throw new InvalidDataException("Static download is too large.");
                    await output.WriteAsync(buffer.AsMemory(0, count), cancellation).ConfigureAwait(false);
                    if (received - reported >= 1024 * 1024)
                    { StatusFormat($"Downloading FC static data… {received / 1048576:N0} MB", length > 0 ? 0.5 * received / length.Value : null); reported = received; }
                }
                if (length is { } expected && received != expected) throw new InvalidDataException("Incomplete static download.");
            }
            var totals = StaticDataDatabase.Import(zip, staging, build, cancellation, (fraction, message) => StatusFormat(message, 0.5 + fraction * 0.5));
            using (var durable = new FileStream(staging, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) durable.Flush(true);
            cancellation.ThrowIfCancellationRequested();
            string fileName = $"sde-{build}-{token}.sqlite"; installed = Path.Combine(_root, fileName);
            File.Move(staging, installed); staging = null;
            var manifest = new Manifest(fileName, build, totals.Datasets, totals.Records);
            pointer = Path.Combine(_root, "current-" + token + ".json.part");
            await File.WriteAllTextAsync(pointer, JsonSerializer.Serialize(manifest), cancellation).ConfigureAwait(false);
            using (var durable = new FileStream(pointer, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) durable.Flush(true);
            var next = new StaticDataDatabase(installed);
            try
            {
                lock (_gate)
                {
                    cancellation.ThrowIfCancellationRequested();
                    File.Move(pointer, Path.Combine(_root, "current.json"), true); pointer = null;
                    _database?.Dispose(); _database = next; _items.Clear(); _simulationCatalog = null;
                    _status = new("Complete static data ready offline.", build, Busy: true, Progress: 1,
                        StoredBytes: new FileInfo(installed).Length, Datasets: totals.Datasets, Records: totals.Records);
                    installed = null;
                }
            }
            catch { next.Dispose(); throw; }
            // Only superseded generated database files inside this service's own root.
            foreach (string old in Directory.EnumerateFiles(_root, "sde-*.sqlite"))
                if (!Path.GetFileName(old).Equals(fileName, StringComparison.OrdinalIgnoreCase)) Delete(old);
            try { DataReplaced?.Invoke(); } catch (Exception ex) { _logger.Warning("Static data subscriber failed ({ErrorType})", ex.GetType().Name); }
            return CommandResult.OkFormat($"Downloaded {totals.Datasets} datasets from FC build {build}.");
        }
        catch (OperationCanceledException)
        { Status(Build is null ? "Static data download cancelled." : "Update cancelled; previous static data is still available."); return CommandResult.Error("Static data update cancelled."); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or HttpRequestException or JsonException or Microsoft.Data.Sqlite.SqliteException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        {
            _logger.Warning(ex, "Static data update failed");
            Status(Build is null ? "Could not download static data. Check your connection and free disk space, then retry."
                : "Could not update static data. The previous copy is still available; retry when ready.");
            return CommandResult.Error(ReadStaticData().Message);
        }
        finally
        {
            Delete(zip); Delete(staging); Delete(staging is null ? null : staging + "-journal"); Delete(installed); Delete(pointer);
            lock (_gate) _status = _status with { Busy = false, Progress = null };
            Notify();
        }
    }
    private static void Delete(string? path)
    { if (path is not null) try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    private void Notify()
    { try { StaticDataChanged?.Invoke(); } catch (Exception ex) { _logger.Warning("Static data presentation failed ({ErrorType})", ex.GetType().Name); } }
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _download?.Cancel(); _database?.Dispose(); _database = null; _items.Clear();
            _catalogStop.Cancel(); _catalogStop.Dispose();
            if (_update is { IsCompleted: false }) _ = _update.ContinueWith(_ => { _http.Dispose(); _download?.Dispose(); }, TaskScheduler.Default);
            else { _http.Dispose(); _download?.Dispose(); }
        }
    }
}
