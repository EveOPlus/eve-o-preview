using Avalonia.Controls;
using Avalonia.Media;
using EveOPreview.Preview;

namespace EveOPreview.UI.Previews;

/// <summary>
/// Transparent candidate host for platform validation. The OS adapter still owns native
/// owner/z-order, click-through and nonactivation rules; this window does not capture clients.
/// </summary>
public sealed class AvaloniaPreviewOverlayWindow : Window
{
    public AvaloniaPreviewOverlay Renderer { get; } = new();

    public AvaloniaPreviewOverlayWindow()
    {
        Title = "EVE-O Preview overlay candidate";
        SystemDecorations = SystemDecorations.None;
        ShowInTaskbar = false;
        ShowActivated = false;
        CanResize = false;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Content = Renderer;
        Closed += (_, _) => Renderer.Dispose();
    }

    public void ResizePixels(PreviewSize size)
    {
        Renderer.Resize(size);
        Width = size.Width / RenderScaling;
        Height = size.Height / RenderScaling;
    }
}
