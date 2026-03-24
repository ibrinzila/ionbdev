namespace EdgeParse.Core.Utils;

using EdgeParse.Core.Models;

/// <summary>
/// XY-Cut++ algorithm for reading order reconstruction.
/// Recursively partitions page into rectangular regions by finding the largest
/// horizontal and vertical gaps, then sorts elements within each region.
/// </summary>
public static class XyCut
{
    private const double QuantizationBucket = 4.0; // pt
    private const double MinVerticalOverlap = 0.35; // 35%
    private const double VerticalGapRatio = 0.5;

    /// <summary>
    /// Sorts content elements into reading order using XY-Cut++.
    /// </summary>
    public static List<ContentElement> Sort(List<ContentElement> elements)
    {
        if (elements.Count <= 1) return elements;

        // Group by page, sort each page, then concatenate
        var byPage = elements.GroupBy(e => e.PageNumber).OrderBy(g => g.Key);
        var result = new List<ContentElement>();

        foreach (var pageGroup in byPage)
        {
            var sorted = SortPage(pageGroup.ToList());
            result.AddRange(sorted);
        }

        return result;
    }

    private static List<ContentElement> SortPage(List<ContentElement> elements)
    {
        if (elements.Count <= 1) return elements;
        return RecursiveXyCut(elements);
    }

    private static List<ContentElement> RecursiveXyCut(List<ContentElement> elements)
    {
        if (elements.Count <= 1) return elements;

        var hGap = FindLargestHorizontalGap(elements);
        var vGap = FindLargestVerticalGap(elements);

        bool useVerticalSplit = ShouldUseVerticalSplit(elements, hGap, vGap);

        if (useVerticalSplit && vGap != null)
        {
            var left = elements.Where(e => e.BBox.CenterX < vGap.Position).ToList();
            var right = elements.Where(e => e.BBox.CenterX >= vGap.Position).ToList();

            if (left.Count > 0 && right.Count > 0)
            {
                var result = new List<ContentElement>();
                result.AddRange(RecursiveXyCut(left));
                result.AddRange(RecursiveXyCut(right));
                return result;
            }
        }

        if (hGap != null)
        {
            var top = elements.Where(e => e.BBox.CenterY >= hGap.Position).ToList();
            var bottom = elements.Where(e => e.BBox.CenterY < hGap.Position).ToList();

            if (top.Count > 0 && bottom.Count > 0)
            {
                var result = new List<ContentElement>();
                result.AddRange(RecursiveXyCut(top));
                result.AddRange(RecursiveXyCut(bottom));
                return result;
            }
        }

        return FallbackSort(elements);
    }

    private static List<ContentElement> FallbackSort(List<ContentElement> elements)
    {
        return elements
            .OrderByDescending(e => QuantizeY(e.BBox.TopY))
            .ThenBy(e => e.BBox.LeftX)
            .ToList();
    }

    private static double QuantizeY(double y)
    {
        return Math.Round(y / QuantizationBucket) * QuantizationBucket;
    }

    private static Gap? FindLargestHorizontalGap(List<ContentElement> elements)
    {
        var sorted = elements.OrderByDescending(e => e.BBox.TopY).ToList();

        double largestGapSize = 0;
        double gapPosition = 0;
        bool found = false;
        double lastBottom = double.MaxValue;

        foreach (var elem in sorted)
        {
            if (lastBottom < double.MaxValue)
            {
                double gap = lastBottom - elem.BBox.TopY;
                if (gap > 0 && gap > largestGapSize)
                {
                    largestGapSize = gap;
                    gapPosition = elem.BBox.TopY + gap / 2;
                    found = true;
                }
            }
            lastBottom = Math.Min(lastBottom, elem.BBox.BottomY);
        }

        return found ? new Gap(largestGapSize, gapPosition) : null;
    }

    private static Gap? FindLargestVerticalGap(List<ContentElement> elements)
    {
        var sorted = elements.OrderBy(e => e.BBox.LeftX).ToList();

        double largestGapSize = 0;
        double gapPosition = 0;
        bool found = false;
        double lastRight = double.MinValue;

        foreach (var elem in sorted)
        {
            if (lastRight > double.MinValue)
            {
                double gap = elem.BBox.LeftX - lastRight;
                if (gap > 0 && gap > largestGapSize)
                {
                    largestGapSize = gap;
                    gapPosition = lastRight + gap / 2;
                    found = true;
                }
            }
            lastRight = Math.Max(lastRight, elem.BBox.RightX);
        }

        return found ? new Gap(largestGapSize, gapPosition) : null;
    }

    private static bool ShouldUseVerticalSplit(
        List<ContentElement> elements, Gap? hGap, Gap? vGap)
    {
        if (vGap == null) return false;
        if (hGap == null) return true;

        var left = elements.Where(e => e.BBox.CenterX < vGap.Position).ToList();
        var right = elements.Where(e => e.BBox.CenterX >= vGap.Position).ToList();

        if (left.Count < 2 || right.Count < 2) return false;

        double leftMinY = left.Min(e => e.BBox.BottomY);
        double leftMaxY = left.Max(e => e.BBox.TopY);
        double rightMinY = right.Min(e => e.BBox.BottomY);
        double rightMaxY = right.Max(e => e.BBox.TopY);

        double overlapMinY = Math.Max(leftMinY, rightMinY);
        double overlapMaxY = Math.Min(leftMaxY, rightMaxY);
        double overlap = Math.Max(0, overlapMaxY - overlapMinY);
        double totalHeight = Math.Max(leftMaxY, rightMaxY) - Math.Min(leftMinY, rightMinY);

        if (totalHeight <= 0) return false;

        double overlapRatio = overlap / totalHeight;
        if (overlapRatio < MinVerticalOverlap) return false;

        return vGap.Size >= hGap.Size * VerticalGapRatio;
    }

    private sealed record Gap(double Size, double Position);
}
