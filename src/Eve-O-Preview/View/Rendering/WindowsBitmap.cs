using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System.Drawing;
using System.Drawing.Imaging;

namespace EveOPreview.View.Rendering;

/// <summary>Copies changed Windows capture/glyph assets once into a retained Avalonia bitmap.</summary>
internal static class WindowsBitmap
{
    internal static Avalonia.Media.Imaging.Bitmap Copy(System.Drawing.Bitmap source)
    {
        var data = source.LockBits(new Rectangle(0, 0, source.Width, source.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        try
        {
            return new Avalonia.Media.Imaging.Bitmap(Avalonia.Platform.PixelFormat.Bgra8888, AlphaFormat.Premul,
                data.Scan0, new PixelSize(source.Width, source.Height), new Vector(96, 96), data.Stride);
        }
        finally { source.UnlockBits(data); }
    }
}
