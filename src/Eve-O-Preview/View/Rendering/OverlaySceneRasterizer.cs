using EveOPreview.Preview;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;

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
    internal static Asset RenderAsset(OverlayScene scene, PreviewSize size, bool drawTitle = true, bool drawStats = true)
    {
        scene = Arrange(scene, size);
        if (!drawTitle) scene = scene with { Title = "", Subtitle = "", ShowTitle = false, CycleSkipped = false };
        if (!drawStats) scene = scene with { Stats = Array.Empty<OverlayStat>() };
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
        using var customStatsFamily = scene.StatsStyle.FontFamily is { } statFamily ? ResolveFamily(statFamily) : null;
        var statsFamily = customStatsFamily ?? family;
        int statsStyle = (int)(scene.StatsStyle.FontStyle ?? font.Style);
        using var format = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near };
        var layout = MeasureTitleLayout(scene, family, fontSize, format);
        RectangleF ink = RectangleF.Empty;
        if (scene.CycleSkipped)
        {
            int markerSize = Math.Clamp((int)Math.Ceiling(fontSize), 12, 22);
            ink = new RectangleF(layout.MarkerX, layout.MarkerY + 1, markerSize + 2, markerSize + 3);
        }
        if (scene.ShowTitle && !string.IsNullOrEmpty(scene.Title))
            AddTextBounds(scene.Title, fontSize, (int)font.Style, layout.TitleX, layout.TitleY, outlineWidth);
        // Augment rows have a fixed line height. Long labels must clip rather
        // than wrap over the next row, matching the portable renderer.
        format.FormatFlags |= StringFormatFlags.NoWrap;
        if (!string.IsNullOrEmpty(scene.Subtitle))
            AddTextBounds(scene.Subtitle, scene.EffectiveSubtitleSize, 0, layout.SubtitleX, layout.SubtitleY, 2);
        int statCount = Math.Min(scene.Stats.Count, MaximumStats);
        float statY = scene.StatsStyle.StartY(size.Height, statCount);
        for (int index = 0; index < statCount && statY < size.Height; index++, statY += scene.StatsStyle.LineHeight)
        {
            var stat = scene.Stats[index];
            if (!stat.Visible) continue;
            float textSize = Math.Clamp(scene.StatsStyle.FontSize, 8, 32), statX = scene.StatsStyle.OffsetX;
            bool first = true;
            foreach (var part in stat.Segments())
            {
                if (!first) statX += textSize * .6f;
                bool prefix = first || part.PrefixIcons;
                int iconWidth = part.Icons().Count() * scene.StatsStyle.LineHeight;
                void Icons()
                {
                    if (iconWidth == 0) return;
                    var bounds = new RectangleF(statX - 2, statY - 2, iconWidth + 4, scene.StatsStyle.LineHeight + 4);
                    ink = ink.IsEmpty ? bounds : RectangleF.Union(ink, bounds);
                    statX += iconWidth;
                }
                if (prefix) Icons();
                AddTextBounds(part.Text, textSize, statsStyle, statX, statY, 2, statsFamily);
                statX += TextAdvance(part.Text, statsFamily, textSize, format, statsStyle);
                if (!prefix) { statX += textSize * .4f; Icons(); }
                first = false;
            }
        }
        if (ink.IsEmpty) return Rectangle.Empty;
        // One extra pixel covers antialias coverage at fractional glyph edges.
        ink.Inflate(1, 1);
        return Rectangle.Intersect(Rectangle.FromLTRB((int)Math.Floor(ink.Left), (int)Math.Floor(ink.Top),
            (int)Math.Ceiling(ink.Right), (int)Math.Ceiling(ink.Bottom)), new Rectangle(0, 0, size.Width, size.Height));

        void AddTextBounds(string text, float textSize, int style, float x, float y, float stroke, FontFamily textFamily = null)
        {
            using var path = new GraphicsPath();
            path.AddString(text.Length > 4096 ? text[..4096] : text, textFamily ?? family, style, textSize,
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

    public static OverlayScene Arrange(OverlayScene scene, PreviewSize size)
    {
        if (scene.LayoutArranged) return scene;
        using var family = ResolveFamily(scene.Font.Family);
        using var statsFamily = ResolveFamily(scene.StatsStyle.FontFamily ?? scene.Font.Family);
        using var format = new StringFormat { FormatFlags = StringFormatFlags.NoWrap };
        int style = (int)(scene.StatsStyle.FontStyle ?? scene.Font.Style);
        float fontSize = Math.Clamp(scene.StatsStyle.FontSize, 8, 32);
        float Width(OverlayStat row)
        {
            float width = 0; bool first = true;
            foreach (var part in row.Segments())
            {
                if (!first) width += fontSize * (part.PrefixIcons ? .6f : 1);
                width += part.Icons().Count() * scene.StatsStyle.LineHeight + TextAdvance(part.Text, statsFamily, fontSize, format, style);
                first = false;
            }
            return width;
        }
        return OverlayLayout.Arrange(scene, size, TextAdvance(scene.Title, family, scene.Font.Size, format, (int)scene.Font.Style),
            TextAdvance(scene.Subtitle, family, scene.EffectiveSubtitleSize, format, 0), scene.Stats.Where(x => x.Visible).Select(Width).DefaultIfEmpty(0).Max());
    }

    public static void Draw(Graphics graphics, OverlayScene scene, PreviewSize size, bool drawTitle = true)
    {
        scene = Arrange(scene, size);
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
            using var customStatsFamily = scene.StatsStyle.FontFamily is { } statFamily ? ResolveFamily(statFamily) : null;
            var statsFamily = customStatsFamily ?? family;
            int statsStyle = (int)(scene.StatsStyle.FontStyle ?? font.Style);
            using var format = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near };
            var layout = MeasureTitleLayout(scene, family, fontSize, format);
            float x = layout.TitleX, y = layout.TitleY;
            if (drawTitle && scene.CycleSkipped)
            {
                int markerSize = Math.Clamp((int)Math.Ceiling(fontSize), 12, 22);
                DrawMarker(graphics, scene.MarkerStyle, Color.FromArgb(unchecked((int)scene.MarkerColor)), layout.MarkerX, layout.MarkerY, markerSize);
            }
            if (drawTitle && scene.ShowTitle && !string.IsNullOrEmpty(scene.Title))
                DrawText(graphics, scene.Title, family, fontSize, (int)font.Style, x, y,
                    Math.Max(1, size.Width - x), Math.Max(1, size.Height - y), format,
                    Color.FromArgb(unchecked((int)scene.EffectiveTitleColor)), Color.FromArgb(unchecked((int)font.Outline)), outlineWidth);

            format.FormatFlags |= StringFormatFlags.NoWrap;
            if (!string.IsNullOrEmpty(scene.Subtitle))
                DrawText(graphics, scene.Subtitle, family, scene.EffectiveSubtitleSize, 0, layout.SubtitleX, layout.SubtitleY,
                    Math.Max(1, size.Width - layout.SubtitleX), Math.Max(1, size.Height - layout.SubtitleY), format,
                    Color.FromArgb(unchecked((int)scene.SubtitleColor)), Color.Black, 2);
            int statCount = Math.Min(scene.Stats.Count, MaximumStats);
            float statY = scene.StatsStyle.StartY(size.Height, statCount);
            for (int index = 0; index < statCount && statY < size.Height; index++)
            {
                var stat = scene.Stats[index];
                if (!stat.Visible) { statY += scene.StatsStyle.LineHeight; continue; }
                float statX = scene.StatsStyle.OffsetX;
                void Icon(OverlaySymbol symbol, uint color)
                {
                    if (symbol == OverlaySymbol.None) return;
                    DrawIcon(graphics, symbol, color, statX, statY, Math.Clamp(scene.StatsStyle.FontSize, 8, 32));
                    statX += scene.StatsStyle.LineHeight;
                }
                float statSize = Math.Clamp(scene.StatsStyle.FontSize, 8, 32);
                bool first = true;
                foreach (var part in stat.Segments())
                {
                    if (!first) statX += statSize * .6f;
                    bool prefix = first || part.PrefixIcons;
                    if (prefix) foreach (var icon in part.Icons()) Icon(icon.Symbol, icon.Color);
                    DrawText(graphics, part.Text, statsFamily, statSize, statsStyle, statX, statY,
                        Math.Max(1, size.Width - statX), Math.Max(1, size.Height - statY), format,
                        Color.FromArgb(unchecked((int)part.Color)), Color.Black, 2);
                    statX += TextAdvance(part.Text, statsFamily, statSize, format, statsStyle);
                    if (!prefix) { statX += statSize * .4f; foreach (var icon in part.Icons()) Icon(icon.Symbol, icon.Color); }
                    first = false;
                }
                statY += scene.StatsStyle.LineHeight;
            }
        }
        finally { graphics.Restore(saved); }
    }

    private static OverlayTitleLayout MeasureTitleLayout(OverlayScene scene, FontFamily family, float size, StringFormat format)
    {
        // Horizontal neighbours stay on one line. Measure only when their placement requires it.
        if (scene.Subtitle.Length > 0 && scene.SubtitlePlacement is SubtitlePlacement.Left or SubtitlePlacement.Right)
            format.FormatFlags |= StringFormatFlags.NoWrap;
        return scene.TitleLayout(scene.SubtitlePlacement == SubtitlePlacement.Right ? TextAdvance(scene.Title, family, size, format, (int)scene.Font.Style) : 0,
            scene.SubtitlePlacement == SubtitlePlacement.Left ? TextAdvance(scene.Subtitle, family, scene.EffectiveSubtitleSize, format, 0) : 0);
    }

    private static FontFamily ResolveFamily(string name)
    {
        try { return new FontFamily(string.IsNullOrWhiteSpace(name) ? "Consolas" : name); }
        catch (ArgumentException) { return new FontFamily("Arial"); }
    }

    private static float TextAdvance(string text, FontFamily family, float size, StringFormat format, int style)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        using var path = new GraphicsPath();
        path.AddString(text.Length > 4096 ? text[..4096] : text, family, style, size, PointF.Empty, format);
        return path.PointCount == 0 ? 0 : path.GetBounds().Right;
    }

    private static void DrawIcon(Graphics graphics, OverlaySymbol symbol, uint color, float x, float y, float size)
    {
        using var contrast = new Pen(Color.Black, 3) { LineJoin = LineJoin.Round };
        using var ink = new Pen(Color.FromArgb(unchecked((int)color)), 1.5f) { LineJoin = LineJoin.Round };
        using var fill = new SolidBrush(ink.Color);
        foreach (float[] polygon in OverlaySymbols.Fills(symbol))
        {
            var points = new PointF[polygon.Length / 2];
            for (int i = 0; i < points.Length; i++) points[i] = new(x + polygon[i * 2] * size / 16, y + polygon[i * 2 + 1] * size / 16);
            graphics.DrawPolygon(contrast, points); graphics.FillPolygon(fill, points);
        }
        foreach (float[] stroke in OverlaySymbols.Strokes(symbol))
        {
            var points = new PointF[stroke.Length / 2];
            for (int i = 0; i < points.Length; i++) points[i] = new(x + stroke[i * 2] * size / 16, y + stroke[i * 2 + 1] * size / 16);
            graphics.DrawLines(contrast, points); graphics.DrawLines(ink, points);
        }
    }

    private static void DrawText(Graphics graphics, string text, FontFamily family, float size, int style,
        float x, float y, float width, float height, StringFormat format, Color foreground, Color outlineColor, float outlineWidth)
    {
        if (string.IsNullOrEmpty(text)) return;
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
