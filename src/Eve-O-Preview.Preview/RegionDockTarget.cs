namespace EveOPreview.Preview;

/// <summary>A region's physical desktop bounds, independent of the settings or window host.</summary>
public readonly record struct RegionDockTarget(string Id, PreviewRect Bounds)
{
    // Pointer proximity makes the destination independent of the dragged preview's size
    // and of where the user grabbed it. Overlaps prefer the nearest region centre.
    public static string? FindClosest(IReadOnlyList<RegionDockTarget> targets, int x, int y, int distance)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(distance);
        string? selected = null;
        double bestGap = double.MaxValue, bestCentre = double.MaxValue;
        foreach (var target in targets)
        {
            var bounds = target.Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0) continue;
            double dx = Math.Max(0d, Math.Max((double)bounds.X - x, (double)x - bounds.X - bounds.Width));
            double dy = Math.Max(0d, Math.Max((double)bounds.Y - y, (double)y - bounds.Y - bounds.Height));
            double gap = dx * dx + dy * dy;
            double centreX = x - ((double)bounds.X + bounds.Width / 2d);
            double centreY = y - ((double)bounds.Y + bounds.Height / 2d);
            double centre = centreX * centreX + centreY * centreY;
            if (gap > (double)distance * distance || gap > bestGap
                || gap == bestGap && (centre > bestCentre || centre == bestCentre && string.CompareOrdinal(target.Id, selected) >= 0)) continue;
            selected = target.Id; bestGap = gap; bestCentre = centre;
        }
        return selected;
    }
}
