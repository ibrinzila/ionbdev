namespace EdgeParse.Core.Models;

/// <summary>
/// Represents a bounding box in PDF coordinate space (72 pt = 1 inch, origin bottom-left).
/// </summary>
public class BoundingBox
{
    public int PageNumber { get; set; }
    public double LeftX { get; set; }
    public double BottomY { get; set; }
    public double RightX { get; set; }
    public double TopY { get; set; }
    public int? LastPageNumber { get; set; }

    public double Width => RightX - LeftX;
    public double Height => TopY - BottomY;
    public double CenterX => (LeftX + RightX) / 2.0;
    public double CenterY => (BottomY + TopY) / 2.0;
    public double Area => Width * Height;

    public BoundingBox() { }

    public BoundingBox(int pageNumber, double leftX, double bottomY, double rightX, double topY)
    {
        PageNumber = pageNumber;
        LeftX = leftX;
        BottomY = bottomY;
        RightX = rightX;
        TopY = topY;
    }

    /// <summary>
    /// Returns the intersection area ratio (0..1) of this box with another.
    /// </summary>
    public double IntersectionRatio(BoundingBox other)
    {
        if (PageNumber != other.PageNumber) return 0;

        double overlapX = Math.Max(0, Math.Min(RightX, other.RightX) - Math.Max(LeftX, other.LeftX));
        double overlapY = Math.Max(0, Math.Min(TopY, other.TopY) - Math.Max(BottomY, other.BottomY));
        double intersectionArea = overlapX * overlapY;

        double thisArea = Area;
        return thisArea > 0 ? intersectionArea / thisArea : 0;
    }

    /// <summary>
    /// Returns whether this box overlaps with another by at least the given ratio.
    /// </summary>
    public bool Overlaps(BoundingBox other, double minRatio = 0.01)
    {
        return IntersectionRatio(other) >= minRatio;
    }

    /// <summary>
    /// Merges this bounding box with another, returning the union.
    /// </summary>
    public BoundingBox Union(BoundingBox other)
    {
        return new BoundingBox(
            Math.Min(PageNumber, other.PageNumber),
            Math.Min(LeftX, other.LeftX),
            Math.Min(BottomY, other.BottomY),
            Math.Max(RightX, other.RightX),
            Math.Max(TopY, other.TopY)
        );
    }

    /// <summary>
    /// Checks if a point is inside this bounding box.
    /// </summary>
    public bool Contains(double x, double y)
    {
        return x >= LeftX && x <= RightX && y >= BottomY && y <= TopY;
    }

    public override string ToString()
    {
        return $"BBox(p{PageNumber}: {LeftX:F1},{BottomY:F1} - {RightX:F1},{TopY:F1})";
    }
}
