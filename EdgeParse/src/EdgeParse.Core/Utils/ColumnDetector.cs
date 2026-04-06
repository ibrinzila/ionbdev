namespace EdgeParse.Core.Utils;

using EdgeParse.Core.Models;

/// <summary>
/// Detects multi-column layout by analyzing X-coordinate distribution of elements.
/// Finds vertical gap zones that indicate column boundaries.
/// </summary>
public static class ColumnDetector
{
    private const double MinGapWidth = 15.0; // pt minimum gap to be considered a column separator
    private const double MinColumnWidth = 50.0; // pt minimum column width

    /// <summary>
    /// Detects column layout for a page based on element positions.
    /// </summary>
    public static ColumnLayout DetectColumns(List<TextChunk> chunks, PageInfo pageInfo)
    {
        var layout = new ColumnLayout { PageNumber = pageInfo.PageNumber };

        if (chunks.Count < 4)
        {
            // Not enough content to detect multi-column layout
            layout.Columns.Add(new ColumnBoundary
            {
                LeftX = pageInfo.EffectiveLeft,
                RightX = pageInfo.EffectiveRight
            });
            return layout;
        }

        // Build X-axis projection profile
        double pageLeft = pageInfo.EffectiveLeft;
        double pageRight = pageInfo.EffectiveRight;
        double pageWidth = pageRight - pageLeft;

        // Create a histogram of X coverage
        int bins = Math.Max(1, (int)(pageWidth / 2.0));
        var histogram = new int[bins];

        foreach (var chunk in chunks)
        {
            int startBin = Math.Max(0, (int)((chunk.BBox.LeftX - pageLeft) / 2.0));
            int endBin = Math.Min(bins - 1, (int)((chunk.BBox.RightX - pageLeft) / 2.0));

            for (int b = startBin; b <= endBin; b++)
            {
                histogram[b]++;
            }
        }

        // Find gaps (runs of empty bins)
        var gaps = new List<(double Start, double End, double Width)>();
        int gapStart = -1;

        for (int b = 0; b < bins; b++)
        {
            if (histogram[b] == 0)
            {
                if (gapStart == -1) gapStart = b;
            }
            else
            {
                if (gapStart != -1)
                {
                    double gapStartX = pageLeft + gapStart * 2.0;
                    double gapEndX = pageLeft + b * 2.0;
                    double gapWidth = gapEndX - gapStartX;

                    if (gapWidth >= MinGapWidth)
                    {
                        gaps.Add((gapStartX, gapEndX, gapWidth));
                    }
                    gapStart = -1;
                }
            }
        }

        if (gaps.Count == 0)
        {
            // Single column
            layout.Columns.Add(new ColumnBoundary
            {
                LeftX = pageLeft,
                RightX = pageRight
            });
        }
        else
        {
            // Create columns from gaps
            double prevRight = pageLeft;
            foreach (var gap in gaps.OrderBy(g => g.Start))
            {
                double colWidth = gap.Start - prevRight;
                if (colWidth >= MinColumnWidth)
                {
                    layout.Columns.Add(new ColumnBoundary
                    {
                        LeftX = prevRight,
                        RightX = gap.Start
                    });
                }
                prevRight = gap.End;
            }

            // Add last column
            if (pageRight - prevRight >= MinColumnWidth)
            {
                layout.Columns.Add(new ColumnBoundary
                {
                    LeftX = prevRight,
                    RightX = pageRight
                });
            }

            // If we only got 1 or 0 columns, fallback to single column
            if (layout.Columns.Count <= 1)
            {
                layout.Columns.Clear();
                layout.Columns.Add(new ColumnBoundary
                {
                    LeftX = pageLeft,
                    RightX = pageRight
                });
            }
        }

        return layout;
    }

    /// <summary>
    /// Detects column layout from text lines (more stable than from chunks).
    /// </summary>
    public static ColumnLayout DetectColumnsFromLines(List<TextLine> lines, PageInfo pageInfo)
    {
        // Convert lines to pseudo-chunks for reuse
        var chunks = lines.Select(l => new TextChunk
        {
            BBox = l.BBox,
            FontSize = l.FontSize
        }).ToList();

        return DetectColumns(chunks, pageInfo);
    }
}
