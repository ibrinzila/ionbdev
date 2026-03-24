using EdgeParse.Core.Models;
using Xunit;

namespace EdgeParse.Tests;

public class BoundingBoxTests
{
    [Fact]
    public void Width_ReturnsCorrectValue()
    {
        var bbox = new BoundingBox(1, 10, 20, 110, 70);
        Assert.Equal(100, bbox.Width);
    }

    [Fact]
    public void Height_ReturnsCorrectValue()
    {
        var bbox = new BoundingBox(1, 10, 20, 110, 70);
        Assert.Equal(50, bbox.Height);
    }

    [Fact]
    public void CenterX_ReturnsCorrectValue()
    {
        var bbox = new BoundingBox(1, 10, 20, 110, 70);
        Assert.Equal(60, bbox.CenterX);
    }

    [Fact]
    public void Area_ReturnsCorrectValue()
    {
        var bbox = new BoundingBox(1, 10, 20, 110, 70);
        Assert.Equal(5000, bbox.Area);
    }

    [Fact]
    public void IntersectionRatio_FullOverlap_ReturnsOne()
    {
        var a = new BoundingBox(1, 10, 10, 100, 100);
        var b = new BoundingBox(1, 10, 10, 100, 100);
        Assert.Equal(1.0, a.IntersectionRatio(b), 2);
    }

    [Fact]
    public void IntersectionRatio_NoOverlap_ReturnsZero()
    {
        var a = new BoundingBox(1, 10, 10, 50, 50);
        var b = new BoundingBox(1, 60, 60, 100, 100);
        Assert.Equal(0.0, a.IntersectionRatio(b), 2);
    }

    [Fact]
    public void IntersectionRatio_DifferentPages_ReturnsZero()
    {
        var a = new BoundingBox(1, 10, 10, 100, 100);
        var b = new BoundingBox(2, 10, 10, 100, 100);
        Assert.Equal(0.0, a.IntersectionRatio(b));
    }

    [Fact]
    public void IntersectionRatio_PartialOverlap()
    {
        var a = new BoundingBox(1, 0, 0, 100, 100);
        var b = new BoundingBox(1, 50, 50, 150, 150);
        // Overlap is 50x50 = 2500, area of a is 10000
        Assert.Equal(0.25, a.IntersectionRatio(b), 2);
    }

    [Fact]
    public void Overlaps_WithSufficientRatio_ReturnsTrue()
    {
        var a = new BoundingBox(1, 0, 0, 100, 100);
        var b = new BoundingBox(1, 50, 50, 150, 150);
        Assert.True(a.Overlaps(b, 0.01));
    }

    [Fact]
    public void Union_CombinesTwoBoxes()
    {
        var a = new BoundingBox(1, 10, 10, 50, 50);
        var b = new BoundingBox(1, 30, 30, 80, 80);
        var union = a.Union(b);

        Assert.Equal(10, union.LeftX);
        Assert.Equal(10, union.BottomY);
        Assert.Equal(80, union.RightX);
        Assert.Equal(80, union.TopY);
    }

    [Fact]
    public void Contains_PointInside_ReturnsTrue()
    {
        var bbox = new BoundingBox(1, 10, 10, 100, 100);
        Assert.True(bbox.Contains(50, 50));
    }

    [Fact]
    public void Contains_PointOutside_ReturnsFalse()
    {
        var bbox = new BoundingBox(1, 10, 10, 100, 100);
        Assert.False(bbox.Contains(5, 5));
    }
}
