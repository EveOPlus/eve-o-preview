using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using EveOPreview.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using EveOPreview.Configuration.Interface;
using Serilog;

namespace EveOPreview.Configuration.Implementation;

/// <summary>Global application preferences are independent of the active gameplay profile.</summary>
public sealed class ApplicationPreferences
{
    // Version 1 requires an explicit theme selection after the Legacy feature freeze.
    // Gameplay profile ConfigVersion is a separate migration contract.
    private const int CurrentConfigVersion = 1;
    private const int ExplicitThemeSelectionVersion = 1;
    private readonly string _path;
    private JObject _settings = new();
    public string Theme { get; private set; } = "Dark";
    public IReadOnlyList<string> ThumbnailMenuOrder { get; private set; } = ThumbnailMenuActions.DefaultOrder;
    public string ThumbnailMenuTheme { get; private set; } = ThumbnailMenuThemes.FollowApp;
    public string FilePath => _path;
    public event Action Changed;

    public ApplicationPreferences(IProfileManager profiles, ILogger logger)
        : this(ResolveFilePath(profiles.ProfileRootDirectory), logger) { }

    public ApplicationPreferences(string path, ILogger logger)
    {
        _path = path;
        try
        {
            if (File.Exists(path))
            {
                _settings = JObject.Parse(File.ReadAllText(path));
                var savedTheme = _settings.Value<string>("Theme");
                if (SettingsVersion >= ExplicitThemeSelectionVersion && IsKnownTheme(savedTheme)) Theme = savedTheme;
                var menuTheme = _settings.Value<string>("ThumbnailMenuTheme");
                if (ThumbnailMenuThemes.IsKnown(menuTheme)) ThumbnailMenuTheme = menuTheme;
                if (_settings["ThumbnailMenuOrder"] is JArray order)
                    ThumbnailMenuOrder = ThumbnailMenuActions.Normalize(order.Where(x => x.Type == JTokenType.String).Values<string>());
            }
        }
        catch (Exception ex) { logger.Warning(ex, "Could not read application preferences; using defaults"); }
    }

    public static bool IsKnownTheme(string theme) => theme is "Light" or "Dark" or "Legacy";

    private int SettingsVersion => _settings["ConfigVersion"]?.Type == JTokenType.Integer
        && int.TryParse(_settings["ConfigVersion"].ToString(), out int version) ? version : 0;

    // Use the profile resolver's portable/installed decision instead of inventing a
    // second directory policy. A read-only portable location falls back to AppData.
    public static string ResolveFilePath(string profileRootDirectory)
    {
        const string fileName = "EVE-O Preview.settings.json";
        string preferred = Path.GetDirectoryName(Path.GetFullPath(profileRootDirectory));
        string fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Eve-O Preview");
        if (CanWriteDirectory(preferred)) return Path.Combine(preferred, fileName);
        return Path.Combine(fallback, fileName);
    }

    private static bool CanWriteDirectory(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            string probe = Path.Combine(directory, ".eve-o-write-" + Guid.NewGuid().ToString("N"));
            using var file = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    public void SetTheme(string theme)
    {
        if (!IsKnownTheme(theme)) throw new ArgumentException("Choose Light, Dark or Legacy.");
        Save("Theme", theme);
        Theme = theme;
        Changed?.Invoke();
    }

    public void SetThumbnailMenuOrder(IEnumerable<string> order)
    {
        var normalized = ThumbnailMenuActions.Normalize(order);
        Save("ThumbnailMenuOrder", new JArray(normalized));
        ThumbnailMenuOrder = normalized;
        Changed?.Invoke();
    }

    public void SetThumbnailMenuTheme(string theme)
    {
        if (!ThumbnailMenuThemes.IsKnown(theme)) throw new ArgumentException("Choose an available menu theme.");
        Save("ThumbnailMenuTheme", theme);
        ThumbnailMenuTheme = theme;
        Changed?.Invoke();
    }

    private void Save(string key, JToken value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path)));
        string temporary = _path + ".tmp";
        try
        {
            var next = (JObject)_settings.DeepClone();
            next["ConfigVersion"] = Math.Max(CurrentConfigVersion, SettingsVersion);
            // Any preference write completes migration with the effective theme.
            // An unrelated menu edit must not resurrect an old saved Legacy choice.
            next["Theme"] = Theme;
            next[key] = value;
            File.WriteAllText(temporary, next.ToString(Formatting.Indented));
            File.Move(temporary, _path, overwrite: true);
            _settings = next;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
