using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using EveOPreview.Configuration.Implementation;
using EveOPreview.UI;
using Serilog;

namespace EveOPreview.Services.Implementation;

/// <summary>Public portraits shared by donation and future character views.</summary>
public sealed class CharacterPortraitCache : IWorkspacePortraitProvider
{
    private static readonly HttpClient SharedClient = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromDays(7);
    private readonly HttpClient _client;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<long, (byte[] Bytes, DateTime Expires)> _memory = new();
    private readonly ConcurrentDictionary<long, Lazy<Task<byte[]>>> _downloads = new();
    private readonly ConcurrentDictionary<long, DateTime> _retryAfter = new();
    public string DirectoryPath { get; }

    public CharacterPortraitCache(ApplicationPreferences preferences, ILogger logger)
        : this(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(preferences.FilePath)), "Cache", "Portraits"), SharedClient, logger) { }

    public CharacterPortraitCache(string directoryPath, HttpClient client, ILogger logger)
    {
        DirectoryPath = Path.GetFullPath(directoryPath);
        _client = client;
        _logger = logger;
    }

    public async Task<byte[]> GetCharacterPortraitAsync(long characterId)
    {
        if (characterId <= 0) return null;
        if (_memory.TryGetValue(characterId, out var saved))
        {
            if (saved.Expires <= DateTime.UtcNow) _ = Refresh(characterId);
            return saved.Bytes;
        }
        try
        {
            var path = FilePath(characterId);
            if (File.Exists(path))
            {
                var bytes = await File.ReadAllBytesAsync(path).ConfigureAwait(false);
                if (IsJpeg(bytes))
                {
                    var expires = File.GetLastWriteTimeUtc(path) + RefreshInterval;
                    _memory[characterId] = (bytes, expires);
                    if (expires <= DateTime.UtcNow) _ = Refresh(characterId);
                    return bytes;
                }
            }
        }
        catch (IOException ex) { _logger.Debug(ex, "Could not read portrait cache for {CharacterId}", characterId); }
        catch (UnauthorizedAccessException ex) { _logger.Debug(ex, "Could not read portrait cache for {CharacterId}", characterId); }
        return await Refresh(characterId).ConfigureAwait(false);
    }

    private string FilePath(long id) => Path.Combine(DirectoryPath, id.ToString(CultureInfo.InvariantCulture) + ".jpg");
    private static bool IsJpeg(byte[] bytes) => bytes.Length >= 4 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[^2] == 0xff && bytes[^1] == 0xd9;

    private Task<byte[]> Refresh(long id)
    {
        if (_retryAfter.TryGetValue(id, out var next) && next > DateTime.UtcNow) return Task.FromResult<byte[]>(null);
        return _downloads.GetOrAdd(id, key => new Lazy<Task<byte[]>>(() => Download(key))).Value;
    }

    private async Task<byte[]> Download(long id)
    {
        try
        {
            var bytes = await _client.GetByteArrayAsync($"https://images.evetech.net/characters/{id.ToString(CultureInfo.InvariantCulture)}/portrait?size=128").ConfigureAwait(false);
            if (!IsJpeg(bytes)) throw new InvalidDataException("Portrait response was not a complete JPEG.");
            _memory[id] = (bytes, DateTime.UtcNow + RefreshInterval);
            _retryAfter.TryRemove(id, out _);
            string temporary = FilePath(id) + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                await File.WriteAllBytesAsync(temporary, bytes).ConfigureAwait(false);
                File.Move(temporary, FilePath(id), overwrite: true);
            }
            catch (IOException ex) { _logger.Debug(ex, "Could not save portrait cache for {CharacterId}", id); }
            catch (UnauthorizedAccessException ex) { _logger.Debug(ex, "Could not save portrait cache for {CharacterId}", id); }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            return bytes;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidDataException)
        {
            _retryAfter[id] = DateTime.UtcNow + TimeSpan.FromHours(1);
            _logger.Debug(ex, "Could not refresh portrait for {CharacterId}; retaining cached image", id);
            return null;
        }
        finally { _downloads.TryRemove(id, out _); }
    }
}
