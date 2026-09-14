namespace EveOPreview.Preview;

/// <summary>Measured placement shared by every renderer. Same-anchor blocks stack;
/// hidden meter rows retain their slots, so firing never moves the title.</summary>
public static class OverlayLayout
{
    public static OverlayScene Arrange(OverlayScene scene, PreviewSize size, float titleWidth, float subtitleWidth, float statsWidth)
    {
        if (scene.LayoutArranged) return scene;
        var local = scene with { Font = scene.Font with { OffsetX = 0, OffsetY = 0 } };
        var title = local.TitleLayout(titleWidth, subtitleWidth);
        bool hasTitle = scene.ShowTitle && scene.Title.Length > 0;
        float titleHeight = hasTitle ? scene.Font.Size * 1.35f + 3 : 0;
        float subtitleHeight = scene.Subtitle.Length > 0 ? scene.EffectiveSubtitleSize * 1.35f + 3 : 0;
        float marker = scene.CycleSkipped ? Math.Clamp(MathF.Ceiling(scene.Font.Size), 12, 22) : 0;
        float width = Math.Max(hasTitle ? title.TitleX + titleWidth : 0,
            Math.Max(subtitleHeight > 0 ? title.SubtitleX + subtitleWidth : 0, marker > 0 ? title.MarkerX + marker : 0));
        float height = Math.Max(hasTitle ? title.TitleY + titleHeight : 0,
            Math.Max(subtitleHeight > 0 ? title.SubtitleY + subtitleHeight : 0, marker > 0 ? title.MarkerY + marker : 0));
        float statsHeight = Math.Min(8, scene.Stats.Count) * scene.StatsStyle.LineHeight;
        bool stack = height > 0 && statsHeight > 0 && scene.TitlePosition == scene.StatsStyle.EffectivePosition;
        int gap = Math.Max(4, scene.StatsStyle.OffsetY);
        float combined = height + statsHeight + (stack ? gap : 0);
        (int X, int Y) Place(OverlayPosition position, float w, float h, int x, int y)
        {
            int column = (int)position % 3, row = (int)position / 3;
            return ((int)Math.Round(column == 0 ? x : column == 1 ? (size.Width - w) / 2 + x : size.Width - w - x),
                (int)Math.Round(row == 0 ? y : row == 1 ? (size.Height - h) / 2 + y : size.Height - h - y));
        }
        var tp = Place(scene.TitlePosition, width, stack ? combined : height, scene.Font.OffsetX, scene.Font.OffsetY);
        var sp = Place(scene.StatsStyle.EffectivePosition, statsWidth, statsHeight, scene.StatsStyle.OffsetX, scene.StatsStyle.OffsetY);
        if (stack) sp.Y = tp.Y + (int)Math.Ceiling(height) + gap;
        return scene with
        {
            Font = scene.Font with { OffsetX = tp.X, OffsetY = tp.Y },
            StatsStyle = scene.StatsStyle with { OffsetX = sp.X, OffsetY = sp.Y, Top = true, Position = OverlayPosition.TopLeft },
            TitlePosition = OverlayPosition.TopLeft, LayoutArranged = true
        };
    }
}
