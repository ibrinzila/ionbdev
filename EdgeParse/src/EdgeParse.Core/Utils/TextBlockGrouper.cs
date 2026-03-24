namespace EdgeParse.Core.Utils;

using EdgeParse.Core.Models;

/// <summary>
/// Groups consecutive TextLines into TextBlocks (paragraph-level regions)
/// based on consistent line spacing, margin alignment, and column membership.
/// </summary>
public static class TextBlockGrouper
{
    private const double MarginTolerance = 10.0; // pt
    private const double LineSpacingVariance = 1.5; // max ratio between adjacent line spacings

    /// <summary>
    /// Groups text lines into text blocks.
    /// </summary>
    public static List<TextBlock> GroupIntoBlocks(List<TextLine> lines)
    {
        if (lines.Count == 0) return new List<TextBlock>();

        // Sort by top Y descending (top-to-bottom reading order)
        var sorted = lines
            .OrderByDescending(l => l.BBox.TopY)
            .ThenBy(l => l.BBox.LeftX)
            .ToList();

        var blocks = new List<TextBlock>();
        var currentBlockLines = new List<TextLine> { sorted[0] };

        for (int i = 1; i < sorted.Count; i++)
        {
            var line = sorted[i];
            var prevLine = sorted[i - 1];

            bool shouldMerge = ShouldMergeLines(prevLine, line);

            if (shouldMerge)
            {
                currentBlockLines.Add(line);
            }
            else
            {
                blocks.Add(BuildBlock(currentBlockLines));
                currentBlockLines = new List<TextLine> { line };
            }
        }

        if (currentBlockLines.Count > 0)
        {
            blocks.Add(BuildBlock(currentBlockLines));
        }

        return blocks;
    }

    private static bool ShouldMergeLines(TextLine prev, TextLine current)
    {
        // Must be on the same page
        if (prev.BBox.PageNumber != current.BBox.PageNumber) return false;

        // Check left margin alignment
        double leftMarginDiff = Math.Abs(prev.BBox.LeftX - current.BBox.LeftX);
        if (leftMarginDiff > MarginTolerance) return false;

        // Check right margin alignment (relaxed)
        double rightMarginDiff = Math.Abs(prev.BBox.RightX - current.BBox.RightX);
        bool rightAligned = rightMarginDiff < MarginTolerance * 2;

        // Check vertical gap - should be reasonable line spacing
        double gap = prev.BBox.BottomY - current.BBox.TopY;
        double avgFontSize = (prev.FontSize + current.FontSize) / 2.0;

        // Gap should be less than 1.5x the font size (typical line spacing)
        if (gap > avgFontSize * 1.5) return false;

        // Negative gap (overlap) is fine for the same block
        if (gap < -avgFontSize * 0.5) return false;

        // Check similar font characteristics
        bool similarFont = Math.Abs(prev.FontSize - current.FontSize) < 1.0 &&
                          Math.Abs(prev.FontWeight - current.FontWeight) < 100;

        return similarFont || (rightAligned && leftMarginDiff < 5.0);
    }

    private static TextBlock BuildBlock(List<TextLine> lines)
    {
        var block = new TextBlock
        {
            Lines = lines
        };
        block.RecalculateBBox();
        return block;
    }
}
