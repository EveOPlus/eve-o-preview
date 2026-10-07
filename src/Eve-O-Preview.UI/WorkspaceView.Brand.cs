using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace EveOPreview.UI;

public sealed partial class WorkspaceView
{
    // User-selected silver concept, with its baked checkerboard made transparent.
    // Preserve this artwork's node proportions, connections, colors and shading;
    // do not tint it for the current theme.
    private static readonly Lazy<Bitmap> BrandArtwork = new(() =>
    {
        using var stream = AssetLoader.Open(new Uri("avares://Eve-O-Preview.UI/Assets/EveOPreviewSilver.png"));
        return new Bitmap(stream);
    });

    private Image BrandLogo()
    {
        var image = new Image
        {
            Name = "eve-o-brand-logo",
            Source = BrandArtwork.Value,
            Width = 36,
            Height = 36,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center
        };
        RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);
        AutomationProperties.SetName(image, L("EVE-O Preview logo"));
        return image;
    }
}
