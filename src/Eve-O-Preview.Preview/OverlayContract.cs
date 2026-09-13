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

/// <summary>Font size retains the historical title's drawing units (preview pixels).</summary>
public sealed record OverlayFont(
    string Family = "Consolas", float Size = 8.25f,
    OverlayFontStyle Style = OverlayFontStyle.Regular,
    uint Foreground = 0xFFA9A9A9, uint Outline = 0xFFFFFFFF,
    float OutlineWidth = 1f, int OffsetX = 8, int OffsetY = 8);

public sealed record OverlayStat(string Label, string Value, uint Color = 0xFFFFFFFF);
public sealed record OverlayBorder(uint Color, PreviewRect InnerBounds);

/// <summary>Immutable presentation state. Producers replace the stats collection rather than mutating it.</summary>
public sealed record OverlayScene
{
    public string Title { get; init; } = "";
    public bool ShowTitle { get; init; } = true;
    public OverlayFont Font { get; init; } = new();
    public bool CycleSkipped { get; init; }
    public CycleMarkerStyle MarkerStyle { get; init; }
    public uint MarkerColor { get; init; } = 0xFFFF0000;
    public IReadOnlyList<OverlayStat> Stats { get; init; } = Array.Empty<OverlayStat>();
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
