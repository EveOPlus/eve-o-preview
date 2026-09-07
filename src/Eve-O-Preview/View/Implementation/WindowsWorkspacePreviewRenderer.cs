using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using EveOPreview.UI;
using EveOPreview.View.CustomControl;

namespace EveOPreview.View;

/// <summary>Renders draft settings with the same GDI+ control used by the live thumbnail overlay.</summary>
public sealed class WindowsWorkspacePreviewRenderer : IWorkspacePreviewRenderer
{
    private static readonly Lazy<IReadOnlyList<string>> InstalledFonts = new(() =>
    {
        using var fonts = new InstalledFontCollection();
        var families = fonts.Families;
        try { return families.Select(f => f.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray(); }
        finally { foreach (var family in families) family.Dispose(); }
    });

    public IReadOnlyList<string> FontFamilies => InstalledFonts.Value;

    public WorkspacePreviewImage RenderPreview(WorkspacePreviewRequest request)
    {
        string Value(string key, string fallback) => request.Settings.TryGetValue(key, out var value) ? value : fallback;
        double Number(string key, string fallback, double minimum, double maximum)
        {
            if (!double.TryParse(Value(key, fallback), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                || !double.IsFinite(value) || value < minimum || value > maximum)
                throw new ArgumentException($"Enter a valid {key} before previewing.");
            return value;
        }
        bool Enabled(string key, bool fallback) => bool.TryParse(Value(key, fallback.ToString()), out var value) ? value : fallback;
        Color ColorValue(string key, string fallback)
        {
            var value = Value(key, fallback);
            if (value.Length != 7 || value[0] != '#' || !int.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
                throw new ArgumentException("Use a six-digit hex color before previewing.");
            return Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
        }

        int width = (int)Number("ThumbnailWidth", "384", 1, 3840);
        int height = (int)Number("ThumbnailHeight", "216", 1, 2160);
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        bitmap.SetResolution(96, 96);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            var background = ColorTranslator.FromHtml(request.BackgroundColor);
            graphics.Clear(background);
            var imageBounds = new Rectangle(0, 0, width, height);
            if (request.Active && Enabled("EnableActiveClientHighlight", false))
            {
                int thickness = (int)Number("ActiveClientHighlightThickness", "3", 1, 6);
                int imageHeight = Math.Max(0, height - 2 * thickness);
                // Same aspect-preserving insets as ThumbnailView.HighlightThumbnail.
                int imageWidth = (int)Math.Round(imageHeight * ((double)width / height), MidpointRounding.AwayFromZero);
                int left = (width - imageWidth) / 2;
                graphics.Clear(ColorValue("ActiveClientHighlightColor", "#ADFF2F"));
                using var imageBrush = new SolidBrush(background);
                graphics.FillRectangle(imageBrush, left, thickness, imageWidth, imageHeight);
                imageBounds = new Rectangle(left, thickness, imageWidth, imageHeight);
            }

            if (request.BackgroundPng is { Length: > 0 } png)
            {
                using var sourceStream = new MemoryStream(png, false);
                using var source = Image.FromStream(sourceStream);
                graphics.DrawImage(source, imageBounds);
            }

            if (Enabled("ShowThumbnailOverlays", true) || request.CycleSkipped)
            {
                if (!Enum.TryParse<FontStyle>(Value("TitleFontStyle", "Regular"), out var style) || ((int)style & ~15) != 0)
                    throw new ArgumentException("Choose a valid title style before previewing.");
                using var font = new Font(Value("TitleFontName", "Arial"), (float)Number("TitleFontSize", "14.25", 1, 200), style);
                if (!font.FontFamily.Name.Equals(Value("TitleFontName", "Arial"), StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Choose an installed font to preview it.");
                using var label = new PreviewLabel
                {
                    AutoSize = true,
                    Font = font,
                    Text = request.Title.Replace("EVE - ", ""),
                    ForeColor = ColorValue("TitleFontForeColor", "#FFA500"),
                    OutlineColor = ColorValue("TitleFontOutlineColor", "#000000"),
                    OutlineWidth = (float)Number("TitleFontOutlineWidth", "3", 0, 20)
                };
                label.SetTitleVisible(Enabled("ShowThumbnailOverlays", true));
                label.SetCycleSkipIndicator(request.CycleSkipped, Value("CycleSkipIndicatorStyle", "Circle with slash"), ColorValue("CycleSkipIndicatorColor", "#FF0000"));
                label.Size = label.GetPreferredSize(Size.Empty);
                int left = (int)Number("TitleFontOffsetLeft", "10", -10000, 10000);
                int top = (int)Number("TitleFontOffsetTop", "5", -10000, 10000);
                graphics.TranslateTransform(left, top);
                graphics.SetClip(label.ClientRectangle, System.Drawing.Drawing2D.CombineMode.Intersect);
                label.PaintTitle(graphics);
            }
        }
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return new(stream.ToArray(), width, height);
    }

    private sealed class PreviewLabel : OutlinedLabel
    {
        public void PaintTitle(Graphics graphics) => base.OnPaint(new PaintEventArgs(graphics, ClientRectangle));
    }
}
