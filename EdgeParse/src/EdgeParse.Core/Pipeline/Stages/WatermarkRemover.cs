namespace EdgeParse.Core.Pipeline.Stages;

using EdgeParse.Core.Models;

/// <summary>
/// Stage 1b: Removes watermark text (repeated content across pages at similar positions).
/// </summary>
public static class WatermarkRemover
{
    private const double PositionTolerance = 5.0; // pt
    private const double MinPageRatio = 0.5; // Must appear on at least 50% of pages

    public static List<PageChunks> RemoveWatermarks(List<PageChunks> pages)
    {
        if (pages.Count < 3) return pages; // Need multiple pages to detect watermarks

        // Collect all text chunks with their positions
        var textByContent = new Dictionary<string, List<(int Page, BoundingBox BBox)>>();

        foreach (var page in pages)
        {
            foreach (var chunk in page.TextChunks)
            {
                var text = chunk.Value.Trim();
                if (string.IsNullOrEmpty(text) || text.Length < 3) continue;

                if (!textByContent.ContainsKey(text))
                    textByContent[text] = new List<(int, BoundingBox)>();

                textByContent[text].Add((page.PageNumber, chunk.BBox));
            }
        }

        // Find text that appears on most pages at similar positions
        var watermarkTexts = new HashSet<string>();
        double minPages = pages.Count * MinPageRatio;

        foreach (var (text, occurrences) in textByContent)
        {
            if (occurrences.Count < minPages) continue;

            // Check if positions are similar across pages
            var avgX = occurrences.Average(o => o.BBox.CenterX);
            var avgY = occurrences.Average(o => o.BBox.CenterY);

            bool consistentPosition = occurrences.All(o =>
                Math.Abs(o.BBox.CenterX - avgX) < PositionTolerance &&
                Math.Abs(o.BBox.CenterY - avgY) < PositionTolerance);

            if (consistentPosition)
            {
                watermarkTexts.Add(text);
            }
        }

        // Remove watermark chunks
        if (watermarkTexts.Count > 0)
        {
            foreach (var page in pages)
            {
                page.TextChunks = page.TextChunks
                    .Where(c => !watermarkTexts.Contains(c.Value.Trim()))
                    .ToList();
            }
        }

        return pages;
    }
}
