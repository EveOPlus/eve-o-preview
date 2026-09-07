using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace EveOPreview.UI;

public sealed partial class WorkspaceView
{
    // Shared embedded artwork avoids network requests and repeated image decoding.
    private static readonly Lazy<Bitmap> PartnerArtwork = new(() =>
    {
        using var stream = AssetLoader.Open(new Uri("avares://Eve-O-Preview.UI/Assets/EvePartner.png"));
        return new Bitmap(stream);
    });

    private static Border PartnerBadge(double width)
    {
        var image = new Image
        {
            Name = "eve-partner-badge",
            Source = PartnerArtwork.Value,
            Width = width,
            Height = width * 1116 / 2658,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);
        AutomationProperties.SetName(image, "EVE Online Partner");
        ToolTip.SetTip(image, "EVE Online Partner");
        // Trim the transparent vertical clear space while preserving artwork proportions.
        image.Margin = new Thickness(0, -width * 0.10, 0, -width * 0.10);
        return new Border
        {
            Background = B("#141B2B"),
            BorderBrush = B("#303B50"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 5),
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Child = image
        };
    }
}
