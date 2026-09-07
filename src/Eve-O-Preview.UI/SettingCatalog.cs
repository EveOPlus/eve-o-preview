using System.Globalization;

namespace EveOPreview.UI;

public enum SettingKind { Toggle, Number, Text, Choice, Color, AudioIds }

public sealed record SettingDefinition(string Key, string Label, string Description, string Page,
    SettingKind Kind, double? Minimum = null, double? Maximum = null,
    IReadOnlyList<string>? Options = null, string Aliases = "")
{
    public bool Matches(string query) => $"{Label} {Description} {Page} {Aliases} {Key}"
        .Contains(query, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Portable metadata for the settings surface and platform-adapter validation.</summary>
public static class SettingCatalog
{
    public static IReadOnlyList<SettingDefinition> All { get; } = new SettingDefinition[]
    {
        new("ProfileAccentColor", "Profile accent color", "Give this profile a distinct color cue. Leave empty to use the selected theme's accent.", "Profiles", SettingKind.Color, Aliases: "colour identity personalization"),
        new("EnableThumbnailSnap", "Snap previews together", "Align a preview with nearby previews when arranging it.", "AdvancedPreview", SettingKind.Toggle, Aliases: "snap placement layout"),
        new("HideDelaySeconds", "Delay before hiding (seconds)", "Wait before hiding previews outside EVE. Rounded up to the next client check; 0 hides on the next check.", "AdvancedPreview", SettingKind.Number, 0, 3600, Aliases: "HideThumbnailsDelay lost focus"),
        new("ThumbnailRefreshPeriod", "Client check interval (ms)", "How often EVE-O discovers clients and updates preview properties. This does not set game FPS or live preview frame rate.", "AdvancedPreview", SettingKind.Number, 300, 1000, Aliases: "refresh polling discovery"),
        new("EnableCompatibilityMode", "Use compatibility capture", "Use still-image previews instead of live previews. Slower updates; useful when live previews do not work.", "AdvancedPreview", SettingKind.Toggle, Aliases: "CompatibilityMode screenshot DWM renderer"),
        new("ThumbnailMinimumWidth", "Minimum preview width (px)", "The smallest width allowed when resizing a preview.", "AdvancedPreview", SettingKind.Number, 1, 960, Aliases: "ThumbnailMinimumSize limits bounds"),
        new("ThumbnailMinimumHeight", "Minimum preview height (px)", "The smallest height allowed when resizing a preview.", "AdvancedPreview", SettingKind.Number, 1, 540, Aliases: "ThumbnailMinimumSize limits bounds"),
        new("ThumbnailMaximumWidth", "Maximum preview width (px)", "The largest width allowed when resizing a preview. Current previews shrink to fit if needed.", "AdvancedPreview", SettingKind.Number, 1, 960, Aliases: "ThumbnailMaximumSize limits bounds"),
        new("ThumbnailMaximumHeight", "Maximum preview height (px)", "The largest height allowed when resizing a preview. Current previews shrink to fit if needed.", "AdvancedPreview", SettingKind.Number, 1, 540, Aliases: "ThumbnailMaximumSize limits bounds"),
        new("LoginThumbnailLeft", "Login preview X (px)", "Horizontal position for new login-screen previews. Negative values support monitors left of the main display.", "AdvancedPreview", SettingKind.Number, -100000, 100000, Aliases: "LoginThumbnailLocation position"),
        new("LoginThumbnailTop", "Login preview Y (px)", "Vertical position for new login-screen previews. You can also drag a login preview into place.", "AdvancedPreview", SettingKind.Number, -100000, 100000, Aliases: "LoginThumbnailLocation position"),
        new("MinimizeToTray", "Close to the system tray", "Keep previews running when you close this window. Use Exit to quit completely.", "General", SettingKind.Toggle, Aliases: "minimise background startup"),
        new("EnableClientLayoutTracking", "Remember game window positions", "Restore each EVE client's saved window location. Turning this off clears saved game window positions.", "General", SettingKind.Toggle, Aliases: "layout tracking"),
        new("EnablePerClientThumbnailLayouts", "Separate preview layouts per character", "Keep a different arrangement for each active character. Turning this off clears those separate layouts.", "General", SettingKind.Toggle, Aliases: "per client layout"),
        new("MinimizeInactiveClients", "Minimize inactive clients", "Minimize other EVE windows when switching. Priority clients keep their existing exception.", "General", SettingKind.Toggle, Aliases: "minimise focus"),
        new("HideActiveClientThumbnail", "Hide the active character's preview", "Show previews only for the other clients while a character is active.", "General", SettingKind.Toggle, Aliases: "focus thumbnail"),
        new("HideThumbnailsOnLostFocus", "Hide previews outside EVE", "Temporarily hide previews when EVE is no longer in focus.", "General", SettingKind.Toggle, Aliases: "lost focus"),
        new("ShowThumbnailsAlwaysOnTop", "Keep previews on top", "Keep your preview windows above other desktop windows.", "Thumbnail", SettingKind.Toggle, Aliases: "topmost z order"),
        new("ThumbnailWidth", "Preview width", "The shared width of your previews, in pixels.", "Thumbnail", SettingKind.Number, 192, 960, Aliases: "thumbnail size geometry"),
        new("ThumbnailHeight", "Preview height", "The shared height of your previews, in pixels.", "Thumbnail", SettingKind.Number, 108, 540, Aliases: "thumbnail size geometry"),
        new("ThumbnailOpacity", "Preview opacity", "20% is mostly transparent; 100% is fully opaque.", "Thumbnail", SettingKind.Number, 20, 100, Aliases: "transparency thumbnail"),
        new("EnableThumbnailZoom", "Zoom previews on hover", "Enlarge a preview when the pointer is over it.", "Zoom", SettingKind.Toggle, Aliases: "magnify"),
        new("ThumbnailZoomFactor", "Zoom magnification", "How much larger the hovered preview becomes.", "Zoom", SettingKind.Number, 2, 10, Aliases: "scale factor"),
        new("ThumbnailZoomAnchor", "Zoom anchor", "The point that stays fixed while a preview grows.", "Zoom", SettingKind.Choice, Options: new[] { "NW", "N", "NE", "W", "C", "E", "SW", "S", "SE" }, Aliases: "direction corner center centre"),
        new("ShowThumbnailOverlays", "Show character names", "Display the character title over each preview.", "Overlay", SettingKind.Toggle, Aliases: "overlay title"),
        new("ShowThumbnailFrames", "Show preview window frames", "Add a standard frame around preview windows.", "Overlay", SettingKind.Toggle, Aliases: "border thumbnail"),
        new("EnableActiveClientHighlight", "Highlight the active character", "Give the active character's preview a distinct border.", "Overlay", SettingKind.Toggle, Aliases: "focus outline"),
        new("ActiveClientHighlightColor", "Active border color", "Enter a six-digit hex color, such as #6D9FFF.", "Overlay", SettingKind.Color, Aliases: "colour highlight"),
        new("ActiveClientHighlightThickness", "Active border thickness", "The top and bottom border width, in pixels. Side borders preserve the preview's aspect ratio.", "Overlay", SettingKind.Number, 1, 6, Aliases: "highlight width outline"),
        new("TitleFontName", "Title font family", "Use an installed font family for the character overlay.", "Overlay", SettingKind.Text, Aliases: "typeface"),
        new("TitleFontSize", "Title font size", "The character title size used by the overlay. Decimal values are supported.", "Overlay", SettingKind.Number, 1, 200, Aliases: "text"),
        new("TitleFontStyle", "Title font style", "Combine bold, italic, underline and strikeout as needed.", "Overlay", SettingKind.Choice, Options: new[] { "Regular", "Bold", "Italic", "Bold, Italic", "Underline", "Bold, Underline", "Italic, Underline", "Bold, Italic, Underline", "Strikeout", "Bold, Strikeout", "Italic, Strikeout", "Bold, Italic, Strikeout", "Underline, Strikeout", "Bold, Underline, Strikeout", "Italic, Underline, Strikeout", "Bold, Italic, Underline, Strikeout" }),
        new("TitleFontForeColor", "Title text color", "The fill color of character names, in #RRGGBB format.", "Overlay", SettingKind.Color, Aliases: "colour foreground"),
        new("TitleFontOutlineColor", "Title outline color", "The outline color around character names, in #RRGGBB format.", "Overlay", SettingKind.Color, Aliases: "colour stroke"),
        new("TitleFontOutlineWidth", "Title outline width", "The stroke around title text. Use 0 for no outline.", "Overlay", SettingKind.Number, 0, 20, Aliases: "thickness"),
        new("TitleFontOffsetLeft", "Title horizontal offset", "Move the title from the left edge, in pixels. Negative values are allowed.", "Overlay", SettingKind.Number, -10000, 10000, Aliases: "position x left"),
        new("TitleFontOffsetTop", "Title vertical offset", "Move the title from the top edge, in pixels. Negative values are allowed.", "Overlay", SettingKind.Number, -10000, 10000, Aliases: "position y top"),
        new("CycleSkipIndicatorStyle", "Skipped character marker", "The symbol beside a character's title while it is skipped in cycle groups.", "Overlay", SettingKind.Choice, Options: new[] { "Circle with slash", "Pause", "Cross" }, Aliases: "disabled skip icon symbol"),
        new("CycleSkipIndicatorColor", "Skip marker color", "The marker remains visible even when character titles are hidden.", "Overlay", SettingKind.Color, Aliases: "disabled skip icon colour"),
        new("FpsEnabled", "Limit client frame rates", "Apply separate limits to the active, background and next predicted client.", "FpsAudio", SettingKind.Toggle, Aliases: "fps throttle limiter"),
        new("FpsFocused", "Active client FPS", "Frame rate while a character has focus. Use 0 for no limit.", "FpsAudio", SettingKind.Number, 0, 1000, Aliases: "foreground focused"),
        new("FpsBackground", "Background client FPS", "Frame rate for other characters. Use 0 for no limit.", "FpsAudio", SettingKind.Number, 0, 1000, Aliases: "inactive"),
        new("FpsPredictingFocus", "Next client FPS", "Keep the predicted next character ready for switching. Use 0 for no limit.", "FpsAudio", SettingKind.Number, 0, 1000, Aliases: "predicted anticipating focus"),
        new("EnableAutomaticCpuAffinity", "Automatic CPU affinity", "Assign processor resources according to the active and predicted client roles.", "FpsAudio", SettingKind.Toggle, Aliases: "performance cores scheduling"),
        new("AudioMuteJumpGateTunnel", "Mute jump gate tunnel", "Silence the jump gate tunnel sound effect.", "FpsAudio", SettingKind.Toggle, Aliases: "audio sound stargate"),
        new("AudioMuteLocationBanner", "Mute Asteroid Belt Warp In", "Silence the asteroid belt warp-in sound effect.", "FpsAudio", SettingKind.Toggle, Aliases: "audio sound notification asteroid belt warp in location banner"),
        new("AudioCustomMutedEventIds", "Custom muted audio event IDs", "Comma-separated unsigned decimal IDs. Empty clears the list; duplicates are removed when applied.", "FpsAudio", SettingKind.AudioIds, Aliases: "advanced wwise sound mute"),
    };

    public static SettingDefinition? Find(string key) => All.FirstOrDefault(s => s.Key == key);

    public static string? Validate(SettingDefinition setting, string value)
    {
        if (setting.Key == "ProfileAccentColor" && string.IsNullOrEmpty(value)) return null;
        if (setting.Kind == SettingKind.Number)
        {
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
                return "Enter a valid number.";
            if ((setting.Minimum is { } minimum && number < minimum) || (setting.Maximum is { } maximum && number > maximum))
                return $"Enter a value from {setting.Minimum} to {setting.Maximum}.";
            if (setting.Key is not ("TitleFontSize" or "TitleFontOutlineWidth" or "HideDelaySeconds") && number != Math.Truncate(number))
                return "Enter a whole number.";
        }
        if (setting.Kind == SettingKind.Color && (value.Length != 7 || value[0] != '#' || !uint.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _)))
            return "Use # followed by six hex digits, for example #6D9FFF.";
        if (setting.Kind == SettingKind.Text && string.IsNullOrWhiteSpace(value))
            return "Enter a value before applying.";
        if (setting.Kind == SettingKind.AudioIds && value.Split(',').Any(t => t.Trim().Length > 0 && !uint.TryParse(t.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out _)))
            return "Each event ID must be a whole number from 0 to 4294967295.";
        return null;
    }
}
