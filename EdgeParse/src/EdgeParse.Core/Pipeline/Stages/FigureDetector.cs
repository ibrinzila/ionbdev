namespace EdgeParse.Core.Pipeline.Stages;

using EdgeParse.Core.Models;

/// <summary>
/// Stage 10b: Groups images and line art into SemanticFigure containers.
/// </summary>
public static class FigureDetector
{
    private const double ProximityThreshold = 20.0; // pt

    public static List<ContentElement> DetectFigures(List<ContentElement> elements)
    {
        var result = new List<ContentElement>();
        var pendingImages = new List<ImageChunk>();
        var pendingLineArts = new List<LineArtChunk>();
        BoundingBox? pendingBBox = null;

        foreach (var elem in elements)
        {
            if (elem.Type == ContentElementType.Image)
            {
                var img = elem.Image!;
                if (pendingBBox != null && IsNearby(pendingBBox, img.BBox))
                {
                    pendingImages.Add(img);
                    pendingBBox = pendingBBox.Union(img.BBox);
                }
                else
                {
                    FlushFigure(result, pendingImages, pendingLineArts, pendingBBox);
                    pendingImages = new List<ImageChunk> { img };
                    pendingLineArts = new List<LineArtChunk>();
                    pendingBBox = img.BBox;
                }
            }
            else if (elem.Type == ContentElementType.LineArt)
            {
                var la = elem.LineArt!;
                if (pendingBBox != null && IsNearby(pendingBBox, la.BBox))
                {
                    pendingLineArts.Add(la);
                    pendingBBox = pendingBBox.Union(la.BBox);
                }
                else if (pendingImages.Count > 0)
                {
                    pendingLineArts.Add(la);
                    pendingBBox = pendingBBox!.Union(la.BBox);
                }
                else
                {
                    result.Add(elem);
                }
            }
            else
            {
                FlushFigure(result, pendingImages, pendingLineArts, pendingBBox);
                pendingImages = new List<ImageChunk>();
                pendingLineArts = new List<LineArtChunk>();
                pendingBBox = null;
                result.Add(elem);
            }
        }

        FlushFigure(result, pendingImages, pendingLineArts, pendingBBox);

        return result;
    }

    private static void FlushFigure(
        List<ContentElement> result,
        List<ImageChunk> images,
        List<LineArtChunk> lineArts,
        BoundingBox? bbox)
    {
        if (images.Count == 0 && lineArts.Count == 0) return;

        var figure = new SemanticFigure
        {
            BBox = bbox ?? new BoundingBox(),
            Images = images,
            LineArts = lineArts
        };

        result.Add(ContentElement.FromFigure(figure));
    }

    private static bool IsNearby(BoundingBox a, BoundingBox b)
    {
        if (a.PageNumber != b.PageNumber) return false;

        double hDist = Math.Max(0, Math.Max(a.LeftX, b.LeftX) - Math.Min(a.RightX, b.RightX));
        double vDist = Math.Max(0, Math.Max(a.BottomY, b.BottomY) - Math.Min(a.TopY, b.TopY));

        return hDist < ProximityThreshold && vDist < ProximityThreshold;
    }
}
