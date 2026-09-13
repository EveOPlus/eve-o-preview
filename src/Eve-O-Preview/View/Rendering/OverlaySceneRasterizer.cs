using EveOPreview.Preview;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace EveOPreview.View.Rendering;

/// <summary>
/// Rasterizes infrequently changing title/stat assets into premultiplied alpha. Native
/// composition retains the result; this is never called by an animation/frame callback.
/// The font size and offsets retain the original GraphicsPath title's pixel units.
/// </summary>
public static class OverlaySceneRasterizer
{
    public const int MaximumStats = 8;

    internal sealed record Asset(Bitmap Bitmap, int X, int Y) : IDisposable
    {
        public void Dispose() => Bitmap.Dispose();
    }

    /// <summary>Only allocates the visible ink bounds, never the full zoomed preview.</summary>
    internal static Asset RenderAsset(OverlayScene scene, PreviewSize size)
    {
        Rectangle bounds = MeasureInk(scene, size);
        // Intersect can return a zero-width/height rectangle at a nonzero origin;
        // Rectangle.IsEmpty does not identify those edge-touching cases.
        if (bounds.Width <= 0 || bounds.Height <= 0) return null;
        var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppPArgb);
        try
        {
            using var graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.Transparent);
            graphics.TranslateTransform(-bounds.X, -bounds.Y);
            Draw(graphics, scene, size);
            return new Asset(bitmap, bounds.X, bounds.Y);
        }
        catch { bitmap.Dispose(); throw; }
    }

    private static Rectangle MeasureInk(OverlayScene scene, PreviewSize size)
    {
        var font = scene.Font;
        float fontSize = float.IsFinite(font.Size) ? Math.Clamp(font.Size, 1, 256) : 8.25f;
        float outlineWidth = float.IsFinite(font.OutlineWidth) ? Math.Clamp(font.OutlineWidth, 0, 32) : 1;
        using var family = ResolveFamily(font.Family);
        using var format = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near };
        RectangleF ink = RectangleF.Empty;
        float titleX = font.OffsetX;
        if (scene.CycleSkipped)
        {
            int markerSize = Math.Clamp((int)Math.Ceiling(fontSize), 12, 22);
            ink = new RectangleF(titleX, font.OffsetY + 1, markerSize + 2, markerSize + 3);
            titleX += markerSize + 5;
        }
        if (scene.ShowTitle && !string.IsNullOrEmpty(scene.Title))
            AddTextBounds(scene.Title, fontSize, (int)font.Style, titleX, font.OffsetY, outlineWidth);
        int statCount = Math.Min(scene.Stats.Count, MaximumStats);
        float statY = Math.Max(0, size.Height - statCount * 20 - 8);
        for (int index = 0; index < statCount && statY < size.Height; index++, statY += 20)
            AddTextBounds($"{scene.Stats[index].Label}: {scene.Stats[index].Value}", 16, 0, 8, statY, 2);
        if (ink.IsEmpty) return Rectangle.Empty;
        // One extra pixel covers antialias coverage at fractional glyph edges.
        ink.Inflate(1, 1);
        return Rectangle.Intersect(Rectangle.FromLTRB((int)Math.Floor(ink.Left), (int)Math.Floor(ink.Top),
            (int)Math.Ceiling(ink.Right), (int)Math.Ceiling(ink.Bottom)), new Rectangle(0, 0, size.Width, size.Height));

        void AddTextBounds(string text, float textSize, int style, float x, float y, float stroke)
        {
            using var path = new GraphicsPath();
            path.AddString(text.Length > 4096 ? text[..4096] : text, family, style, textSize,
                new RectangleF(x, y, Math.Max(1, size.Width - x), Math.Max(1, size.Height - y)), format);
            if (path.PointCount == 0) return;
            using var pen = new Pen(Color.Black, Math.Max(0.1f, stroke)) { LineJoin = LineJoin.Round, Alignment = PenAlignment.Outset };
            var textBounds = path.GetBounds(null, pen);
            ink = ink.IsEmpty ? textBounds : RectangleF.Union(ink, textBounds);
        }
    }

    public static Bitmap Render(OverlayScene scene, PreviewSize size)
    {
        var bitmap = new Bitmap(Math.Max(1, size.Width), Math.Max(1, size.Height), PixelFormat.Format32bppPArgb);
        try
        {
            using var graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.Transparent);
            Draw(graphics, scene, size);
            return bitmap;
        }
        catch { bitmap.Dispose(); throw; }
    }

    public static void Draw(Graphics graphics, OverlayScene scene, PreviewSize size)
    {
        var font = scene.Font;
        float fontSize = float.IsFinite(font.Size) ? Math.Clamp(font.Size, 1, 256) : 8.25f;
        float outlineWidth = float.IsFinite(font.OutlineWidth) ? Math.Clamp(font.OutlineWidth, 0, 32) : 1;
        var saved = graphics.Save();
        try
        {
            graphics.SetClip(new Rectangle(0, 0, size.Width, size.Height));
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.CompositingMode = CompositingMode.SourceOver;
            using var family = ResolveFamily(font.Family);
            using var format = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near };
            float x = font.OffsetX, y = font.OffsetY;
            if (scene.CycleSkipped)
            {
                int markerSize = Math.Clamp((int)Math.Ceiling(fontSize), 12, 22);
                DrawMarker(graphics, scene.MarkerStyle, Color.FromArgb(unchecked((int)scene.MarkerColor)), x, y, markerSize);
                x += markerSize + 5;
            }
            if (scene.ShowTitle && !string.IsNullOrEmpty(scene.Title))
                DrawText(graphics, scene.Title, family, fontSize, (int)font.Style, x, y,
                    Math.Max(1, size.Width - x), Math.Max(1, size.Height - y), format,
                    Color.FromArgb(unchecked((int)font.Foreground)), Color.FromArgb(unchecked((int)font.Outline)), outlineWidth);

            int statCount = Math.Min(scene.Stats.Count, MaximumStats);
            float statY = Math.Max(0, size.Height - statCount * 20 - 8);
            for (int index = 0; index < statCount && statY < size.Height; index++)
            {
                var stat = scene.Stats[index];
                DrawText(graphics, $"{stat.Label}: {stat.Value}", family, 16, 0,
                    8, statY, Math.Max(1, size.Width - 8), Math.Max(1, size.Height - statY), format,
                    Color.FromArgb(unchecked((int)stat.Color)), Color.Black, 2);
                statY += 20;
            }
        }
        finally { graphics.Restore(saved); }
    }

    private static FontFamily ResolveFamily(string name)
    {
        try { return new FontFamily(string.IsNullOrWhiteSpace(name) ? "Consolas" : name); }
        catch (ArgumentException) { return new FontFamily("Arial"); }
    }

    private static void DrawText(Graphics graphics, string text, FontFamily family, float size, int style,
        float x, float y, float width, float height, StringFormat format, Color foreground, Color outlineColor, float outlineWidth)
    {
        using var path = new GraphicsPath();
        // Bound producer text independently of the graphics surface size.
        if (text.Length > 4096) text = text[..4096];
        path.AddString(text, family, style, size, new RectangleF(x, y, width, height), format);
        if (outlineWidth > 0.1f)
        {
            using var outline = new Pen(outlineColor, outlineWidth) { LineJoin = LineJoin.Round, Alignment = PenAlignment.Outset };
            graphics.DrawPath(outline, path);
        }
        using var brush = new SolidBrush(foreground);
        graphics.FillPath(brush, path);
    }

    private static void DrawMarker(Graphics graphics, CycleMarkerStyle style, Color color, float x, float y, int size)
    {
        using var path = new GraphicsPath();
        var box = new RectangleF(x + 2, y + 3, size - 3, size - 3);
        switch (style)
        {
            case CycleMarkerStyle.Pause:
                path.AddLine(box.Left + 2, box.Top, box.Left + 2, box.Bottom);
                path.StartFigure(); path.AddLine(box.Right - 2, box.Top, box.Right - 2, box.Bottom);
                break;
            case CycleMarkerStyle.Cross:
                path.AddLine(box.Left, box.Top, box.Right, box.Bottom);
                path.StartFigure(); path.AddLine(box.Right, box.Top, box.Left, box.Bottom);
                break;
            default:
                path.AddEllipse(box);
                path.StartFigure(); path.AddLine(box.Left + 2, box.Top + 2, box.Right - 2, box.Bottom - 2);
                break;
        }
        using var contrast = new Pen(Color.Black, 4) { LineJoin = LineJoin.Round };
        using var foreground = new Pen(color, 2) { LineJoin = LineJoin.Round };
        graphics.DrawPath(contrast, path);
        graphics.DrawPath(foreground, path);
    }
}
