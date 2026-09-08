using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EveOPreview.Configuration.Implementation;
using EveOPreview.UI;
using Serilog;

namespace EveOPreview.Services.Implementation;

/// <summary>Global derived identity map. Launch credentials never enter this service.</summary>
public sealed class CharacterIdentityCache : IWorkspaceCharacterProvider, IDisposable
{
    public sealed record KnownCharacter(string Name, long? CharacterId, long? EveUserId,
        DateTimeOffset? CharacterCheckedAt, DateTimeOffset? UserCheckedAt);
    private static readonly HttpClient SharedClient = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);
    private readonly object _gate = new();
    private readonly Dictionary<string, KnownCharacter> _known = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _live = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _characterRetry = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(string Name, int Pid), DateTimeOffset> _userRetry = new();
    private readonly ConcurrentDictionary<string, Lazy<Task<WorkspaceCharacter>>> _work = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _requests = new(2), _writes = new(1);
    private readonly CancellationTokenSource _stop = new();
    private readonly HttpClient _client;
    private readonly Func<int, string, long?> _readUserId;
    private readonly Func<DateTimeOffset> _now;
    private readonly ILogger _logger;
    private readonly Timer _timer;
    private DateTimeOffset _esiRetry;
    private volatile bool _disposed;
    public string FilePath { get; }
    public event Action Changed;

    public CharacterIdentityCache(ApplicationPreferences preferences, EveClientUserIdReader reader, ILogger logger)
        : this(Path.Combine(Path.GetDirectoryName(preferences.FilePath), "Cache", "Characters.json"), SharedClient,
            reader.Read, logger, () => DateTimeOffset.UtcNow)
    {
        _timer = new Timer(_ => RefreshKnown(), null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
    }

    public CharacterIdentityCache(string path, HttpClient client, Func<int, string, long?> readUserId,
        ILogger logger, Func<DateTimeOffset> now)
    {
        FilePath = Path.GetFullPath(path); _client = client; _readUserId = readUserId; _logger = logger; _now = now;
        try
        {
            if (!File.Exists(FilePath) || new FileInfo(FilePath).Length > 2 * 1024 * 1024) return;
            var entries = JsonSerializer.Deserialize<KnownCharacter[]>(File.ReadAllBytes(FilePath));
            foreach (var entry in (entries ?? []).Take(5000))
                if (entry is not null && ValidName(entry.Name) && (entry.CharacterId is null or > 0) && (entry.EveUserId is null or > 0))
                    _known[entry.Name] = entry;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        { _logger.Debug("Character identity cache could not be read; it will be rebuilt."); }
    }

    public IReadOnlyDictionary<string, KnownCharacter> KnownCharacters
    { get { lock (_gate) return new Dictionary<string, KnownCharacter>(_known, StringComparer.OrdinalIgnoreCase); } }

    private static bool ValidName(string name) => !string.IsNullOrWhiteSpace(name) && name.Length <= 100 && !name.Any(char.IsControl);
    private static string CharacterName(string title) => title?.StartsWith("EVE - ", StringComparison.Ordinal) == true
        && ValidName(title[6..]) ? title[6..] : null;
    private static WorkspaceCharacter Display(KnownCharacter entry) => new(entry.Name, entry.CharacterId, entry.EveUserId);

    // Called after discovery releases its process-cache lock. No process or HTTP reads occur here.
    public void ObserveProcesses(ICollection<IProcessInfo> processes)
    {
        if (_disposed) return;
        lock (_gate)
        {
            _live.Clear();
            foreach (var process in processes)
                if (CharacterName(process.Title) is { } name) _live[name] = process.ProcessId;
            foreach (var key in _userRetry.Keys.Where(k => !_live.TryGetValue(k.Name, out var pid) || pid != k.Pid).ToArray()) _userRetry.Remove(key);
        }
        foreach (var process in processes) _ = GetCharacterAsync(process.Title);
    }

    public Task<WorkspaceCharacter> GetCharacterAsync(string fullTitle)
    {
        string name = CharacterName(fullTitle);
        if (_disposed || name is null) return Task.FromResult<WorkspaceCharacter>(null);
        KnownCharacter entry;
        bool needed;
        lock (_gate)
        {
            if (!_known.TryGetValue(name, out entry))
            {
                if (_known.Count >= 5000) return Task.FromResult<WorkspaceCharacter>(null);
                _known[name] = entry = new(name, null, null, null, null);
            }
            var now = _now();
            bool characterDue = (entry.CharacterCheckedAt is null || now - entry.CharacterCheckedAt >= Lifetime)
                && _characterRetry.GetValueOrDefault(name) <= now && _esiRetry <= now;
            bool userDue = _live.TryGetValue(name, out var pid) && (entry.UserCheckedAt is null || now - entry.UserCheckedAt >= Lifetime)
                && _userRetry.GetValueOrDefault((name, pid)) <= now;
            needed = characterDue || userDue;
        }
        if (!needed) return Task.FromResult(Display(entry));
        var task = _work.GetOrAdd(name, _ => new Lazy<Task<WorkspaceCharacter>>(() => Task.Run(() => Refresh(name)))).Value;
        // Keep usable identities and portraits available while refreshing old data.
        return entry.CharacterId.HasValue ? Task.FromResult(Display(entry)) : task;
    }

    private async Task<WorkspaceCharacter> Refresh(string name)
    {
        bool entered = false, notify = false;
        try
        {
            await _requests.WaitAsync(_stop.Token).ConfigureAwait(false); entered = true;
            KnownCharacter before;
            int pid;
            bool characterDue, userDue;
            var now = _now();
            lock (_gate)
            {
                before = _known[name]; _live.TryGetValue(name, out pid);
                characterDue = (before.CharacterCheckedAt is null || now - before.CharacterCheckedAt >= Lifetime)
                    && _characterRetry.GetValueOrDefault(name) <= now && _esiRetry <= now;
                userDue = pid > 0 && (before.UserCheckedAt is null || now - before.UserCheckedAt >= Lifetime)
                    && _userRetry.GetValueOrDefault((name, pid)) <= now;
                if (characterDue) _characterRetry[name] = now.AddHours(1);
                if (userDue) _userRetry[(name, pid)] = now.AddHours(1);
            }
            long? userId = null, characterId = null;
            if (userDue)
            {
                try { userId = _readUserId(pid, "EVE - " + name); }
                catch { /* Never log command-line reader exceptions or their data. */ }
            }
            if (characterDue) characterId = await Lookup(name).ConfigureAwait(false);
            KnownCharacter updated;
            lock (_gate)
            {
                updated = _known[name];
                if (characterId is > 0)
                {
                    if (updated.CharacterId.HasValue && updated.CharacterId != characterId)
                        updated = updated with { EveUserId = null, UserCheckedAt = null };
                    updated = updated with { CharacterId = characterId, CharacterCheckedAt = now };
                }
                // Reject a result if discovery changed the character/process association during the read.
                if (userId is > 0 && _live.TryGetValue(name, out int currentPid) && currentPid == pid)
                    updated = updated with { EveUserId = userId, UserCheckedAt = now };
                _known[name] = updated;
            }
            if (updated != before && !_disposed)
            {
                await Save().ConfigureAwait(false);
                notify = true;
            }
            return Display(updated);
        }
        catch (OperationCanceledException) { return null; }
        finally
        {
            if (entered) _requests.Release();
            _work.TryRemove(name, out _);
            if (notify && !_disposed) Changed?.Invoke();
        }
    }

    private async Task<long?> Lookup(string name)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://esi.evetech.net/universe/ids");
            request.Headers.TryAddWithoutValidation("X-Compatibility-Date", "2026-09-07");
            request.Headers.UserAgent.ParseAdd("EVE-O-Preview/" + typeof(CharacterIdentityCache).Assembly.GetName().Version);
            request.Content = new StringContent(JsonSerializer.Serialize(new[] { name }), Encoding.UTF8, "application/json");
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, _stop.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                lock (_gate)
                {
                    if ((int)response.StatusCode == 404) _characterRetry[name] = _now().AddHours(12);
                    else
                    {
                        var retry = response.Headers.RetryAfter?.Date ?? _now() + (response.Headers.RetryAfter?.Delta ?? TimeSpan.FromHours(1));
                        if (response.Headers.TryGetValues("X-Esi-Error-Limit-Reset", out var values)
                            && int.TryParse(values.FirstOrDefault(), out int seconds)) retry = Max(retry, _now().AddSeconds(Math.Clamp(seconds, 1, 86400)));
                        _esiRetry = Max(_now().AddMinutes(1), retry);
                    }
                }
                return null;
            }
            if (response.Content.Headers.ContentLength > 1024 * 1024) return null;
            await response.Content.LoadIntoBufferAsync(1024 * 1024, _stop.Token).ConfigureAwait(false);
            using var json = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(_stop.Token).ConfigureAwait(false));
            if (json.RootElement.TryGetProperty("characters", out var characters) && characters.ValueKind == JsonValueKind.Array)
                foreach (var character in characters.EnumerateArray())
                    if (character.TryGetProperty("name", out var label) && label.ValueKind == JsonValueKind.String
                        && string.Equals(label.GetString(), name, StringComparison.OrdinalIgnoreCase)
                        && character.TryGetProperty("id", out var id) && id.TryGetInt64(out long number) && number > 0) return number;
            lock (_gate) _characterRetry[name] = _now().AddHours(12);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException)
        {
            lock (_gate) _esiRetry = _now().AddHours(1);
            _logger.Debug("Public character lookup unavailable; retaining cached identities.");
        }
        return null;
    }

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;
    private async Task Save()
    {
        await _writes.WaitAsync(_stop.Token).ConfigureAwait(false);
        string temporary = FilePath + ".tmp";
        try
        {
            KnownCharacter[] snapshot;
            lock (_gate) snapshot = _known.Values.Where(e => e.CharacterId.HasValue || e.EveUserId.HasValue).ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            await File.WriteAllBytesAsync(temporary, JsonSerializer.SerializeToUtf8Bytes(snapshot), _stop.Token).ConfigureAwait(false);
            File.Move(temporary, FilePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { _logger.Debug("Character identity cache could not be saved; retaining in-memory identities."); }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            _writes.Release();
        }
    }

    private void RefreshKnown()
    {
        string[] names;
        lock (_gate) names = _known.Keys.ToArray();
        foreach (string name in names) _ = GetCharacterAsync("EVE - " + name);
    }

    public void Dispose() { _disposed = true; _timer?.Dispose(); _stop.Cancel(); }
}
