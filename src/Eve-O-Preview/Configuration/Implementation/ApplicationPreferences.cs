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
    public string UiLanguage { get; private set; } = "auto";
    public IReadOnlyList<string> ThumbnailMenuOrder { get; private set; } = ThumbnailMenuActions.DefaultOrder;
    public string ThumbnailMenuTheme { get; private set; } = ThumbnailMenuThemes.FollowApp;
    public string PreviewOverlayRenderer { get; private set; } = "NativeComposition";
    // Diagnostic state is session-only; it must never silently carry into ordinary use after restart.
    public bool DiagnosticHotkeyPassthrough { get; private set; }
    public CombatLogSettings CombatLogs { get; private set; } = new();
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
                UiLanguage = WorkspaceLocalization.NormalizePreference(_settings.Value<string>("UiLanguage"));
                var savedTheme = _settings.Value<string>("Theme");
                if (SettingsVersion >= ExplicitThemeSelectionVersion && IsKnownTheme(savedTheme)) Theme = savedTheme;
                var menuTheme = _settings.Value<string>("ThumbnailMenuTheme");
                if (ThumbnailMenuThemes.IsKnown(menuTheme)) ThumbnailMenuTheme = menuTheme;
                var previewRenderer = _settings.Value<string>("PreviewOverlayRenderer");
                if (IsKnownPreviewOverlayRenderer(previewRenderer)) PreviewOverlayRenderer = previewRenderer;
                if (_settings["CombatLogs"] is JObject logs)
                {
                    try { CombatLogs = NormalizeCombatLogs(logs.ToObject<CombatLogSettings>()); }
                    catch (ArgumentException) { logger.Warning("Invalid combat log settings; using defaults"); }
                }
                if (_settings["ThumbnailMenuOrder"] is JArray order)
                    ThumbnailMenuOrder = ThumbnailMenuActions.Normalize(order.Where(x => x.Type == JTokenType.String).Values<string>());
            }
        }
        catch (Exception ex) { logger.Warning(ex, "Could not read application preferences; using defaults"); }
    }

    public static bool IsKnownTheme(string theme) => theme is "Light" or "Dark" or "Legacy";
    public static bool IsKnownPreviewOverlayRenderer(string renderer) => renderer is "NativeComposition" or "Legacy";

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

    public void SetLanguage(string language)
    {
        string normalized = WorkspaceLocalization.NormalizePreference(language);
        Save("UiLanguage", normalized);
        UiLanguage = normalized;
        Changed?.Invoke();
    }

    // Compatibility with development profiles created before hotkey settings became profile-owned.
    // Explicit profile values always win; the next profile save writes both fields independently.
    internal void ApplyLegacyHotkeyDefaults(JObject profile, IThumbnailConfiguration configuration)
    {
        if (profile.Property("UseWindowsHotkeys", StringComparison.OrdinalIgnoreCase) == null)
            configuration.UseWindowsHotkeys = _settings["UseWindowsHotkeys"]?.Type == JTokenType.Boolean && _settings.Value<bool>("UseWindowsHotkeys");
        if (profile.Property("GlobalHotkeysOnRelease", StringComparison.OrdinalIgnoreCase) == null)
            configuration.GlobalHotkeysOnRelease = _settings["GlobalHotkeysOnRelease"]?.Type == JTokenType.Boolean && _settings.Value<bool>("GlobalHotkeysOnRelease");
    }

    public void SetDiagnosticHotkeyPassthrough(bool enabled)
    {
        if (DiagnosticHotkeyPassthrough == enabled) return;
        DiagnosticHotkeyPassthrough = enabled;
        Changed?.Invoke();
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

    public void SetPreviewOverlayRenderer(string renderer)
    {
        if (!IsKnownPreviewOverlayRenderer(renderer)) throw new ArgumentException("Choose an available preview graphics mode.");
        if (PreviewOverlayRenderer == renderer) return;
        Save("PreviewOverlayRenderer", renderer);
        PreviewOverlayRenderer = renderer;
        Changed?.Invoke();
    }

    public void SetCombatLogs(CombatLogSettings settings)
    {
        var normalized = NormalizeCombatLogs(settings);
        Save("CombatLogs", JObject.FromObject(normalized));
        CombatLogs = normalized;
        Changed?.Invoke();
    }

    public static CombatLogSettings NormalizeCombatLogs(CombatLogSettings settings)
    {
        if (settings is null || settings.WindowSeconds is < 1 or > 300 || settings.RetentionDays is < 1 or > 30)
            throw new ArgumentException("Use a DPS window of 1–300 seconds and retention of 1–30 days.");
        if (settings.FlashSeconds is < 1 or > 10 || settings.FlashColor is not { Length: 7 } || settings.FlashColor[0] != '#'
            || !uint.TryParse(settings.FlashColor.AsSpan(1), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out _))
            throw new ArgumentException("Choose a flash duration of 1–10 seconds and a colour such as #FF7666.");
        if (settings.FlashIntervalMilliseconds is < 100 or > 2000)
            throw new ArgumentException("Choose a flash interval of 100–2000 ms.");
        if (!Enum.IsDefined(settings.FlashTarget)) throw new ArgumentException("Choose a flash target.");
        if (!Enum.IsDefined(settings.Language)) throw new ArgumentException("Choose a log language.");
        if (!Enum.IsDefined(settings.FlashAnimation)) throw new ArgumentException("Choose a flash animation.");
        if (settings.FlashOpacityPercent is < 0 or > 100) throw new ArgumentException("Choose a flash opacity of 0–100%.");
        string directory = settings.Directory?.Trim() ?? "";
        if (directory.Length > 0)
        {
            if (!Path.IsPathFullyQualified(directory)) throw new ArgumentException("Choose an absolute EVE logs folder path.");
            directory = Path.GetFullPath(directory);
        }
        static LogOverlayOptions Validate(LogOverlayOptions options)
        {
            if (options is not null && (!Enum.IsDefined(options.TitlePosition) || !Enum.IsDefined(options.RowOrder)
                || options.Position is { } position && !Enum.IsDefined(position))) throw new ArgumentException("Choose a valid augment position and order.");
            if (options is null || options.FontSize is < 8 or > 32 || options.OffsetX is < 0 or > 500 || options.OffsetY is < 0 or > 500
                || options.EventDurationSeconds is < 1 or > 15 || options.Preset is not ("Classic" or "Damage colours" or "Minimal"))
                throw new ArgumentException("Overlay font must be 8–32 pixels; offsets must be 0–500 pixels.");
            static string Color(string value)
            {
                if (value is null || value.Length != 7 || value[0] != '#' || !uint.TryParse(value.AsSpan(1),
                    System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out _))
                    throw new ArgumentException("Use colour values such as #66AAFF.");
                return value.ToUpperInvariant();
            }
            static IReadOnlyDictionary<TKey, CombatVisualStyle> Styles<TKey>(IReadOnlyDictionary<TKey, CombatVisualStyle> styles) where TKey : struct, Enum
            {
                if (styles is null || styles.Count > Enum.GetValues<TKey>().Length) throw new ArgumentException("Invalid overlay style settings.");
                return new System.Collections.ObjectModel.ReadOnlyDictionary<TKey, CombatVisualStyle>(styles.ToDictionary(x => x.Key, x =>
                {
                    if (!Enum.IsDefined(x.Key) || x.Value is null || !Enum.IsDefined(x.Value.Icon)) throw new ArgumentException("Choose an available overlay icon.");
                    return x.Value with { Color = Color(x.Value.Color), TextColor = x.Value.TextColor is null ? null : Color(x.Value.TextColor) };
                }));
            }
            var appearance = options.CustomAppearance;
            if (!Enum.IsDefined(options.SystemPlacement) || options.SystemFontSize is { } systemSize && (!float.IsFinite(systemSize) || systemSize < 1 || systemSize > 200))
                throw new ArgumentException("Choose a valid system position and font size.");
            if (appearance is not null)
            {
                if (!Enum.IsDefined(appearance.TextColorMode)) throw new ArgumentException("Choose an available text colour mode.");
                appearance = appearance with { IncomingColor = Color(appearance.IncomingColor), OutgoingColor = Color(appearance.OutgoingColor), SystemColor = Color(appearance.SystemColor),
                    Damage = Styles(appearance.Damage), Weapons = Styles(appearance.Weapons), Repairs = Styles(appearance.Repairs) };
            }
            string family = options.FontFamily?.Trim();
            if (family is { Length: > 200 } || family?.Any(char.IsControl) == true
                || options.FontStyle is { } fontStyle && ((int)fontStyle & ~15) != 0)
                throw new ArgumentException("Choose a valid font family and style.");
            return options with { CustomAppearance = appearance, SystemColor = options.SystemColor is null ? null : Color(options.SystemColor),
                FontFamily = string.IsNullOrEmpty(family) ? null : family };
        }
        if (settings.Overlays is null || settings.Overlays.Count > 1000) throw new ArgumentException("Too many character overlay settings.");
        var overlays = new Dictionary<string, LogOverlayOptions>(StringComparer.Ordinal);
        foreach (var pair in settings.Overlays)
        {
            if (!pair.Key.StartsWith("EVE - ", StringComparison.Ordinal) || pair.Key.Length is < 7 or > 106)
                throw new ArgumentException("Overlay settings require a full character window title.");
            overlays[pair.Key] = Validate(pair.Value);
        }
        return settings with { Directory = directory, DefaultOverlay = Validate(settings.DefaultOverlay),
            Overlays = new System.Collections.ObjectModel.ReadOnlyDictionary<string, LogOverlayOptions>(overlays) };
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
