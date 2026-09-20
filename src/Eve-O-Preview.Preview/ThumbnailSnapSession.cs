namespace EveOPreview.Preview;

/// <summary>A visible neighbour in desktop pixels. Identity is stable for one drag.</summary>
public readonly record struct ThumbnailSnapTarget(long Id, PreviewRect Bounds);

/// <summary>A desktop-pixel alignment line joining the moving and neighbouring edges.</summary>
public readonly record struct ThumbnailSnapGuide(bool Vertical, int Coordinate, int Start, int End);

public readonly record struct ThumbnailSnapResult(PreviewRect Bounds, ThumbnailSnapGuide? VerticalGuide,
    ThumbnailSnapGuide? HorizontalGuide);

/// <summary>
/// Magnetic edge alignment during a drag. Always pass the raw pointer-derived rectangle,
/// never the previous snapped rectangle: a retained edge must not consume pointer travel.
/// Distances are supplied in pixels by the host so its monitor DPI is applied once.
/// </summary>
public sealed class ThumbnailSnapSession
{
    private Edge? _horizontal;
    private Edge? _vertical;

    public void Reset() => (_horizontal, _vertical) = (null, null);

    public ThumbnailSnapResult Move(PreviewRect rawBounds, IReadOnlyList<ThumbnailSnapTarget> targets,
        int acquireDistance, int releaseDistance, bool bypass = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(acquireDistance);
        if (releaseDistance < acquireDistance) throw new ArgumentOutOfRangeException(nameof(releaseDistance));
        if (bypass)
        {
            Reset();
            return new(rawBounds, null, null);
        }

        _horizontal = SelectEdge(rawBounds, targets, true, _horizontal, acquireDistance, releaseDistance);
        _vertical = SelectEdge(rawBounds, targets, false, _vertical, acquireDistance, releaseDistance);
        var snapped = rawBounds with
        {
            X = _horizontal?.Position ?? rawBounds.X,
            Y = _vertical?.Position ?? rawBounds.Y
        };
        return new(snapped, Guide(_horizontal, snapped, true), Guide(_vertical, snapped, false));
    }

    private static Edge? SelectEdge(PreviewRect raw, IReadOnlyList<ThumbnailSnapTarget> targets,
        bool horizontal, Edge? retained, int acquire, int release)
    {
        if (retained is { } previous)
        {
            foreach (var target in targets)
            {
                if (target.Id != previous.TargetId) continue;
                int coordinate = Coordinate(target.Bounds, horizontal, previous.TargetTrailing);
                int position = coordinate - (previous.MovingTrailing ? Length(raw, horizontal) : 0);
                if (Math.Abs((long)position - Origin(raw, horizontal)) <= release
                    && OrthogonalGap(raw, target.Bounds, horizontal) <= release)
                    return previous with { Position = position, Coordinate = coordinate, TargetBounds = target.Bounds };
                break;
            }
            // Give the pointer one free update after breakaway. Nearby competing
            // edges cannot immediately steal the released axis in a dense layout.
            return null;
        }

        Edge? best = null;
        long bestDistance = (long)acquire + 1;
        foreach (var target in targets)
        {
            if (target.Bounds.Width <= 0 || target.Bounds.Height <= 0
                || OrthogonalGap(raw, target.Bounds, horizontal) > acquire) continue;
            for (int moving = 0; moving < 2; moving++)
            for (int destination = 0; destination < 2; destination++)
            {
                int coordinate = Coordinate(target.Bounds, horizontal, destination != 0);
                int position = coordinate - (moving != 0 ? Length(raw, horizontal) : 0);
                long distance = Math.Abs((long)position - Origin(raw, horizontal));
                if (distance > acquire || distance > bestDistance
                    || (distance == bestDistance && best is { } selected && target.Id >= selected.TargetId)) continue;
                bestDistance = distance;
                best = new(target.Id, moving != 0, destination != 0, position, coordinate, target.Bounds);
            }
        }
        return best;
    }

    private static ThumbnailSnapGuide? Guide(Edge? edge, PreviewRect moving, bool horizontal)
    {
        if (edge is not { } value) return null;
        int start = Math.Min(Origin(moving, !horizontal), Origin(value.TargetBounds, !horizontal));
        int end = Math.Max(Coordinate(moving, !horizontal, true), Coordinate(value.TargetBounds, !horizontal, true));
        return new(horizontal, value.Coordinate, start, end);
    }

    private static long OrthogonalGap(PreviewRect a, PreviewRect b, bool horizontal) => Math.Max(0L,
        Math.Max((long)Origin(a, !horizontal) - Coordinate(b, !horizontal, true),
            (long)Origin(b, !horizontal) - Coordinate(a, !horizontal, true)));

    private static int Origin(PreviewRect bounds, bool horizontal) => horizontal ? bounds.X : bounds.Y;
    private static int Length(PreviewRect bounds, bool horizontal) => horizontal ? bounds.Width : bounds.Height;
    private static int Coordinate(PreviewRect bounds, bool horizontal, bool trailing) =>
        Origin(bounds, horizontal) + (trailing ? Length(bounds, horizontal) : 0);

    private readonly record struct Edge(long TargetId, bool MovingTrailing, bool TargetTrailing,
        int Position, int Coordinate, PreviewRect TargetBounds);
}
