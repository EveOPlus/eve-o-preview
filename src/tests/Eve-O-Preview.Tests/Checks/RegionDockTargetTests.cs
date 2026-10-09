using EveOPreview.Preview;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class RegionDockTargetTests
{
    private static readonly RegionDockTarget[] Targets = [new("left", new(-400, -200, 300, 200)), new("right", new(100, 50, 400, 300))];

    [Theory]
    [InlineData(-250, -100, "left")]
    [InlineData(-88, -50, "left")]
    [InlineData(-87, -50, null)]
    [InlineData(300, 200, "right")]
    [InlineData(508, 358, "right")]
    [InlineData(510, 360, null)]
    [InlineData(0, 0, null)]
    public void PointerProximityUsesPhysicalBoundsAndEuclideanCornerDistance(int x, int y, string expected) =>
        Assert.Equal(expected, RegionDockTarget.FindClosest(Targets, x, y, 12));

    [Fact]
    public void OverlappingTargetsPreferNearestCentreThenStableId()
    {
        RegionDockTarget[] targets = [new("b", new(0, 0, 400, 300)), new("a", new(0, 0, 400, 300)), new("near", new(200, 50, 100, 100))];
        Assert.Equal("a", RegionDockTarget.FindClosest(targets, 20, 20, 12));
        Assert.Equal("near", RegionDockTarget.FindClosest(targets, 250, 100, 12));
    }
}
