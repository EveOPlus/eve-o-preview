namespace EveOPreview.Preview;

/// <summary>Native pixels in the preview's client area, independent of settings-window DPI.</summary>
public readonly record struct PreviewSize(int Width, int Height);
public readonly record struct PreviewRect(int X, int Y, int Width, int Height);

[Flags]
public enum OverlayCapabilities
{
    None = 0,
    Title = 1,
    CycleMarker = 2,
    Stats = 4,
    Pulse = 8,
    Shake = 16,
    CompositorAnimations = 32,
    ActiveBorder = 64,
}

[Flags]
public enum OverlayFontStyle { Regular = 0, Bold = 1, Italic = 2, Underline = 4, Strikeout = 8 }
public enum CycleMarkerStyle { CircleSlash, Pause, Cross }
public enum SubtitlePlacement { Below, Above, Left, Right }
public enum OverlayPosition { TopLeft, TopCenter, TopRight, MiddleLeft, MiddleCenter, MiddleRight, BottomLeft, BottomCenter, BottomRight }
public readonly record struct OverlayTitleLayout(float TitleX, float TitleY, float SubtitleX, float SubtitleY, float MarkerX, float MarkerY);

/// <summary>Font size retains the historical title's drawing units (preview pixels).</summary>
public sealed record OverlayFont(
    string Family = "Consolas", float Size = 8.25f,
    OverlayFontStyle Style = OverlayFontStyle.Regular,
    uint Foreground = 0xFFA9A9A9, uint Outline = 0xFFFFFFFF,
    float OutlineWidth = 1f, int OffsetX = 8, int OffsetY = 8);

public sealed record OverlayStat(string Label, string Value, uint Color = 0xFFFFFFFF,
    OverlaySymbol Icon = OverlaySymbol.None, uint IconColor = 0xFFFFFFFF,
    OverlaySymbol SecondaryIcon = OverlaySymbol.None, uint SecondaryIconColor = 0xFFFFFFFF,
    OverlaySymbol ThirdIcon = OverlaySymbol.None, uint ThirdIconColor = 0xFFFFFFFF,
    OverlaySymbol FourthIcon = OverlaySymbol.None, uint FourthIconColor = 0xFFFFFFFF,
    OverlaySymbol FifthIcon = OverlaySymbol.None, uint FifthIconColor = 0xFFFFFFFF)
{
    /// <summary>Hidden rows reserve their line without drawing any ink.</summary>
    public bool Visible { get; init; } = true;
    /// <summary>Optional text and icons appended on the same line, with their own colour.</summary>
    public OverlayStat? Suffix { get; init; }
    /// <summary>Appended compact pairs put their icon before the amount; alpha keeps trailing icons.</summary>
    public bool PrefixIcons { get; init; }
    public IEnumerable<OverlayStat> Segments()
    {
        for (OverlayStat? part = this; part is { Visible: true }; part = part.Suffix) yield return part;
    }
    public string Text => Label.Length == 0 ? Value : Value.Length == 0 ? Label : Label + ": " + Value;
    public IEnumerable<(OverlaySymbol Symbol, uint Color)> Icons()
    {
        if (Icon != OverlaySymbol.None) yield return (Icon, IconColor);
        if (SecondaryIcon != OverlaySymbol.None) yield return (SecondaryIcon, SecondaryIconColor);
        if (ThirdIcon != OverlaySymbol.None) yield return (ThirdIcon, ThirdIconColor);
        if (FourthIcon != OverlaySymbol.None) yield return (FourthIcon, FourthIconColor);
        if (FifthIcon != OverlaySymbol.None) yield return (FifthIcon, FifthIconColor);
    }
}
public sealed record OverlayStatsStyle(int FontSize = 16, int OffsetX = 8, int OffsetY = 8, bool Top = false)
{
    public OverlayPosition? Position { get; init; }
    public OverlayPosition EffectivePosition => Position ?? (Top ? OverlayPosition.TopLeft : OverlayPosition.BottomLeft);
    /// <summary>Null inherits the title's family/style; size and colours remain independent.</summary>
    public string? FontFamily { get; init; }
    public OverlayFontStyle? FontStyle { get; init; }
    public int LineHeight => Math.Clamp(FontSize, 8, 32) + 4;
    public int StartY(int height, int count) => Top ? OffsetY : Math.Max(0, height - count * LineHeight - OffsetY);
}
public sealed record OverlayBorder(uint Color, PreviewRect InnerBounds);

/// <summary>Immutable presentation state. Producers replace the stats collection rather than mutating it.</summary>
public sealed record OverlayScene
{
    public string Title { get; init; } = "";
    public bool ShowTitle { get; init; } = true;
    public OverlayFont Font { get; init; } = new();
    public OverlayPosition TitlePosition { get; init; }
    public bool LayoutArranged { get; init; }
    /// <summary>Transient title highlight; never replaces the configured font colour.</summary>
    public uint? TitleColor { get; init; }
    /// <summary>Transient ARGB wash over the image; text and selection borders remain above it.</summary>
    public uint? DamageTint { get; init; }
    /// <summary>Shared finite damage animation strength. Renderers retain geometry between frames.</summary>
    public double DamageFlashIntensity { get; init; } = 1;
    public uint EffectiveTitleColor => TitleColor is { } color ? OverlayColors.Blend(Font.Foreground, color, DamageFlashIntensity) : Font.Foreground;
    public uint? EffectiveDamageTint => DamageTint is { } tint ? (tint & 0xFFFFFFu) | (uint)Math.Round((tint >> 24) * Math.Clamp(DamageFlashIntensity, 0, 1)) << 24 : null;
    /// <summary>Plain system name positioned relative to the title, independent of combat rows.</summary>
    public string Subtitle { get; init; } = "";
    public uint SubtitleColor { get; init; } = 0xFFD4E8FF;
    public SubtitlePlacement SubtitlePlacement { get; init; }
    public float? SubtitleFontSize { get; init; }
    public float SubtitleY => TitleLayout(0, 0).SubtitleY;
    public float EffectiveSubtitleSize => Math.Clamp(SubtitleFontSize is { } size && float.IsFinite(size) ? size
        : float.IsFinite(Font.Size) ? Font.Size : 8.25f, 1, 256);

    /// <summary>Renderers supply measured text advances; the combined title/system block stays at the configured offsets.</summary>
    public OverlayTitleLayout TitleLayout(float titleWidth, float subtitleWidth)
    {
        float size = Math.Clamp(float.IsFinite(Font.Size) ? Font.Size : 8.25f, 1, 256);
        float marker = CycleSkipped ? Math.Clamp((float)Math.Ceiling(size), 12, 22) + 5 : 0;
        bool title = ShowTitle && Title.Length > 0;
        float x = Font.OffsetX, y = Font.OffsetY, sx = x, sy = y;
        if (Subtitle.Length > 0 && (title || CycleSkipped))
        {
            float gap = Math.Max(6, size * .5f);
            switch (SubtitlePlacement)
            {
                case SubtitlePlacement.Above: y += EffectiveSubtitleSize * 1.35f + 3; break;
                case SubtitlePlacement.Left: x += subtitleWidth + gap; break;
                case SubtitlePlacement.Right: sx += marker + (title ? titleWidth : 0) + gap; break;
                default: sy += Math.Max(title ? size * 1.35f + 3 : 0, CycleSkipped ? marker : 0); break;
            }
        }
        return new(x + marker, y, sx, sy, x, y);
    }
    public bool CycleSkipped { get; init; }
    public CycleMarkerStyle MarkerStyle { get; init; }
    public uint MarkerColor { get; init; } = 0xFFFF0000;
    public IReadOnlyList<OverlayStat> Stats { get; init; } = Array.Empty<OverlayStat>();
    public OverlayStatsStyle StatsStyle { get; init; } = new();
    /// <summary>Optional fixed clip for alert graphics, leaving host borders unobscured. Null uses the entire preview.</summary>
    public PreviewRect? AlertBounds { get; init; }
    /// <summary>Retained selection frame above alerts; changing it must not resize the game image.</summary>
    public OverlayBorder? ActiveBorder { get; init; }
}

/// <summary>A finite visual signal; receiving it never changes source-window focus or saved geometry.</summary>
public sealed record PreviewAlert(
    uint Color = 0xFFFF4038,
    double DurationSeconds = 0.8,
    double Intensity = 0.3,
    double ShakePixels = 0,
    bool ReducedMotion = false)
{
    public PreviewAlert Normalize() => this with
    {
        DurationSeconds = double.IsFinite(DurationSeconds) ? Math.Clamp(DurationSeconds, 0.05, 10) : 0.8,
        Intensity = double.IsFinite(Intensity) ? Math.Clamp(Intensity, 0, 1) : 0.3,
        ShakePixels = ReducedMotion ? 0 : double.IsFinite(ShakePixels) ? Math.Clamp(ShakePixels, 0, 24) : 0,
    };
}

/// <summary>
/// Called on the owning UI thread. Backends own graphics resources and may use different
/// native presentation paths; no frame stream, native handle or UI toolkit crosses this boundary.
/// Hide/clear/dispose must cancel transient visuals. Repeated unchanged state must not redraw.
/// </summary>
public interface IOverlayRenderer : IDisposable
{
    OverlayCapabilities Capabilities { get; }
    void Resize(PreviewSize size);
    void SetScene(OverlayScene scene);
    void SetOpacity(double opacity);
    void SetVisible(bool visible);
    void ShowAlert(PreviewAlert alert);
    void ClearAlerts();
}
