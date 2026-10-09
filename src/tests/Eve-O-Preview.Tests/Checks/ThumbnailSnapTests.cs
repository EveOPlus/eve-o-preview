using EveOPreview.Preview;
using Xunit;

namespace EveOPreview.Tests.Checks;

public class ThumbnailSnapTests
{
    private static readonly ThumbnailSnapTarget[] Neighbours = [new(1, new(300, 100, 200, 100))];

    [Theory]
    [InlineData(-1914, 100, -1920, 100)]
    [InlineData(-500, -194, -500, -200)]
    [InlineData(-500, 774, -500, 780)]
    [InlineData(-194, 300, -200, 300)]
    [InlineData(6, 300, 0, 300)]
    [InlineData(500, 6, 500, 0)]
    [InlineData(1714, 300, 1720, 300)]
    [InlineData(500, 974, 500, 980)]
    public void EachMonitorEdgeAndSharedSeamSnapsInPhysicalPixels(int x, int y, int expectedX, int expectedY)
    {
        ThumbnailSnapTarget[] screens = [new(long.MinValue, new(-1920, -200, 1920, 1080), true),
            new(long.MinValue + 1, new(0, 0, 1920, 1080), true)];
        var raw = new PreviewRect(x, y, 200, 100);
        var session = new ThumbnailSnapSession();
        Assert.Equal(new PreviewRect(expectedX, expectedY, 200, 100), session.Move(raw, screens, 8, 16).Bounds);
        Assert.Equal(raw, session.Move(raw, screens, 8, 16, bypass: true).Bounds);
        Assert.Equal(new PreviewRect(6, 2000, 200, 100), session.Move(new(6, 2000, 200, 100), screens, 8, 16).Bounds);
    }

    [Fact]
    public void FixedMonitorTrailingEdgesRemainTargetsDuringResizeAll()
    {
        var screen = new ThumbnailSnapTarget(long.MinValue, new(0, 0, 1920, 1080), true);
        var result = new ThumbnailSnapSession().Resize(new(100, 100, 1814, 974), [screen], 8, 16,
            false, true, false, true, leadingTargetsOnly: true);
        Assert.Equal(new PreviewRect(100, 100, 1820, 980), result.Bounds);
        var limited = result.WithConstrainedBounds(new(100, 100, 960, 540), false, false);
        Assert.Null(limited.VerticalGuide); Assert.Null(limited.HorizontalGuide);
    }

    [Fact]
    public void ResizeSnapsOnlyDraggedEdgesAndKeepsOppositeCornerFixed()
    {
        var session = new ThumbnailSnapSession();
        var snapped = session.Resize(new(100, 100, 194, 95), Neighbours, 8, 16, false, true, false, true);
        Assert.Equal(new PreviewRect(100, 100, 200, 100), snapped.Bounds);
        Assert.NotNull(snapped.VerticalGuide);
        Assert.Equal(200, session.Resize(new(100, 100, 187, 95), Neighbours, 8, 16, false, true, false, true).Bounds.Width);
        Assert.Equal(183, session.Resize(new(100, 100, 183, 95), Neighbours, 8, 16, false, true, false, true).Bounds.Width);
        session.Reset();
        var leading = session.Resize(new(306, 105, 194, 195), Neighbours, 8, 16, true, false, true, false);
        Assert.Equal(new PreviewRect(300, 100, 200, 200), leading.Bounds);
        var bypass = session.Resize(new(306, 105, 194, 195), Neighbours, 8, 16, true, false, true, false, true);
        Assert.Equal(new PreviewRect(306, 105, 194, 195), bypass.Bounds);
        Assert.Null(bypass.VerticalGuide);
    }

    [Fact]
    public void DragAcquiresImmediatelyAndRetainsUntilPointerBreakaway()
    {
        var session = new ThumbnailSnapSession();
        var acquired = session.Move(new(94, 120, 200, 100), Neighbours, 8, 16);
        Assert.Equal(100, acquired.Bounds.X);
        Assert.Equal(120, acquired.Bounds.Y);
        Assert.Equal(new ThumbnailSnapGuide(true, 300, 100, 220), acquired.VerticalGuide);
        Assert.Equal(100, session.Move(new(87, 120, 200, 100), Neighbours, 8, 16).Bounds.X);
        var released = session.Move(new(83, 120, 200, 100), Neighbours, 8, 16);
        Assert.Equal(83, released.Bounds.X);
        Assert.Null(released.VerticalGuide);
    }

    [Fact]
    public void ShiftBypassClearsLocksAndResumesFromRawPointerPosition()
    {
        var session = new ThumbnailSnapSession();
        session.Move(new(94, 120, 200, 100), Neighbours, 8, 16);
        var bypass = session.Move(new(94, 120, 200, 100), Neighbours, 8, 16, bypass: true);
        Assert.Equal(94, bypass.Bounds.X);
        Assert.Null(bypass.VerticalGuide);
        Assert.Equal(87, session.Move(new(87, 120, 200, 100), Neighbours, 8, 16).Bounds.X);
        Assert.Equal(100, session.Move(new(95, 120, 200, 100), Neighbours, 8, 16).Bounds.X);
    }

    [Fact]
    public void ChoosesNearestEdgeIndependentOfEnumerationAndIgnoresDistantRows()
    {
        ThumbnailSnapTarget[] targets = [new(4, new(305, 100, 200, 100)), Neighbours[0], new(0, new(297, 900, 200, 100))];
        var forward = new ThumbnailSnapSession().Move(new(98, 120, 200, 100), targets, 8, 16);
        System.Array.Reverse(targets);
        var reverse = new ThumbnailSnapSession().Move(new(98, 120, 200, 100), targets, 8, 16);
        Assert.Equal(100, forward.Bounds.X);
        Assert.Equal(forward, reverse);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void SignedCoordinatesAndDpiScaledDistancesKeepPixelGeometry(double scale)
    {
        int acquire = (int)System.Math.Round(8 * scale);
        int release = (int)System.Math.Round(16 * scale);
        var target = new ThumbnailSnapTarget(7, new(-500, -300, 240, 160));
        var raw = new PreviewRect(-700 - acquire, -300 - acquire, 200, 120);
        var result = new ThumbnailSnapSession().Move(raw, [target], acquire, release);
        Assert.Equal(new PreviewRect(-700, -300, 200, 120), result.Bounds);
        Assert.NotNull(result.VerticalGuide);
        Assert.NotNull(result.HorizontalGuide);
    }

    [Fact]
    public void RemovedOrNoLongerAdjacentTargetReleasesWithoutAJump()
    {
        var session = new ThumbnailSnapSession();
        session.Move(new(94, 120, 200, 100), Neighbours, 8, 16);
        Assert.Equal(94, session.Move(new(94, 120, 200, 100), [], 8, 16).Bounds.X);
        session.Move(new(94, 120, 200, 100), Neighbours, 8, 16);
        var movedAway = session.Move(new(94, 240, 200, 100), Neighbours, 8, 16);
        Assert.Equal(94, movedAway.Bounds.X);
        Assert.Null(movedAway.VerticalGuide);
    }
}
