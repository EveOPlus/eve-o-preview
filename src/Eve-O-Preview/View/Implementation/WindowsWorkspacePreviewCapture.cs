using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EveOPreview.Services;
using EveOPreview.UI;
using Serilog;

namespace EveOPreview.View;

/// <summary>Reuses the compatibility capture path for one settings sample, without touching live previews.</summary>
public sealed class WindowsWorkspacePreviewCapture(IProcessMonitor processes, IWindowManager windows, ILogger logger) : IWorkspacePreviewCapture
{
    public Task<WorkspaceClientStill> CapturePreviewStillAsync(string preferredTitle) => Task.Run(() =>
    {
        // Discovery owns this cache. Do not enumerate processes, inject, restore or focus a game here.
        var candidates = processes.GetAllProcesses()
            .Where(client => client.MainWindowHandle != IntPtr.Zero && client.Title.StartsWith("EVE - ", StringComparison.Ordinal))
            .OrderByDescending(client => client.Title == preferredTitle).ThenBy(client => client.Title, StringComparer.OrdinalIgnoreCase);
        foreach (var client in candidates)
        {
            try
            {
                if (windows.IsWindowMinimized(client.MainWindowHandle)) continue;
                using var source = windows.GetStaticThumbnail(client.MainWindowHandle);
                if (source is null) continue;
                double scale = Math.Min(1, Math.Min(960.0 / source.Width, 540.0 / source.Height));
                using var image = new Bitmap(Math.Max(1, (int)(source.Width * scale)), Math.Max(1, (int)(source.Height * scale)));
                using (var graphics = Graphics.FromImage(image))
                {
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(source, new Rectangle(0, 0, image.Width, image.Height));
                }
                // Some GPU/minimized clients return an empty black frame. Keep the readable neutral sample then.
                bool hasContent = false;
                for (int y = 0; y < image.Height && !hasContent; y += Math.Max(1, image.Height / 24))
                    for (int x = 0; x < image.Width && !hasContent; x += Math.Max(1, image.Width / 32))
                    {
                        var pixel = image.GetPixel(x, y);
                        hasContent = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B)) > 8;
                    }
                if (!hasContent) continue;
                using var stream = new MemoryStream();
                image.Save(stream, ImageFormat.Png);
                return new WorkspaceClientStill(client.Title, stream.ToArray());
            }
            catch (Exception ex) { logger.Debug(ex, "Could not capture settings sample for {Title}", client.Title); }
        }
        return null;
    });
}
