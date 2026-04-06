namespace EdgeParse.Core.Utils;

using EdgeParse.Core.Models;

/// <summary>
/// Groups consecutive TextLines into TextBlocks (paragraph-level regions)
/// based on consistent line spacing, margin alignment, column membership, and font characteristics.
/// </summary>
public static class TextBlockGrouper
{
    private const double MarginTolerance = 10.0; // pt
    private const double LineSpacingMax = 1.5; // max gap relative to font size

    public static List<TextBlock> GroupIntoBlocks(List<TextLine> lines)
    {
        if (lines.Count == 0) return new List<TextBlock>();

        // Sort by top Y descending (top-to-bottom reading order)
        var sorted = lines
            .OrderByDescending(l => l.BBox.TopY)
            .ThenBy(l => l.BBox.LeftX)
            .ToList();

        var blocks = new List<TextBlock>();
        var currentBlockLines = new List<TextLine>(16) { sorted[0] };

        for (int i = 1; i < sorted.Count; i++)
        {
            var line = sorted[i];
            var prevLine = sorted[i - 1];

            if (ShouldMergeLines(prevLine, line))
            {
                currentBlockLines.Add(line);
            }
            else
            {
                blocks.Add(BuildBlock(currentBlockLines));
                currentBlockLines = new List<TextLine>(16) { line };
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
        if (prev.BBox.PageNumber != current.BBox.PageNumber) return false;

        // Left margin alignment
        double leftMarginDiff = Math.Abs(prev.BBox.LeftX - current.BBox.LeftX);
        if (leftMarginDiff > MarginTolerance) return false;

        // Vertical gap check
        double gap = prev.BBox.BottomY - current.BBox.TopY;
        double avgFontSize = (prev.FontSize + current.FontSize) / 2.0;

        if (gap > avgFontSize * LineSpacingMax) return false;
        if (gap < -avgFontSize * 0.5) return false;

        // Font similarity check
        bool similarFont = Math.Abs(prev.FontSize - current.FontSize) < 1.0 &&
                          Math.Abs(prev.FontWeight - current.FontWeight) < 100;

        // Right margin alignment (relaxed)
        double rightMarginDiff = Math.Abs(prev.BBox.RightX - current.BBox.RightX);
        bool rightAligned = rightMarginDiff < MarginTolerance * 2;

        return similarFont || (rightAligned && leftMarginDiff < 5.0);
    }

    private static TextBlock BuildBlock(List<TextLine> lines)
    {
        var block = new TextBlock { Lines = lines };
        block.RecalculateBBox();
        return block;
    }
}
