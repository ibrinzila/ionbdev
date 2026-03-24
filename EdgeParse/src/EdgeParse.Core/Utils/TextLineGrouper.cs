namespace EdgeParse.Core.Utils;

using EdgeParse.Core.Models;

/// <summary>
/// Groups TextChunks into TextLines based on shared baselines (Y-position tolerance).
/// Inserts spaces when gap > font_size * 0.17.
/// </summary>
public static class TextLineGrouper
{
    private const double BaselineToleranceFactor = 0.3; // fraction of font size
    private const double SpaceGapFactor = 0.17;

    /// <summary>
    /// Groups text chunks on a page into text lines.
    /// </summary>
    public static List<TextLine> GroupIntoLines(
        List<TextChunk> chunks, ColumnLayout? columnLayout = null)
    {
        if (chunks.Count == 0) return new List<TextLine>();

        // Sort by Y (top-to-bottom) then X (left-to-right)
        var sorted = chunks
            .OrderByDescending(c => c.BBox.TopY)
            .ThenBy(c => c.BBox.LeftX)
            .ToList();

        var lines = new List<TextLine>();
        var currentLineChunks = new List<TextChunk> { sorted[0] };
        double currentBaseline = sorted[0].BBox.BottomY;

        for (int i = 1; i < sorted.Count; i++)
        {
            var chunk = sorted[i];
            double tolerance = chunk.FontSize * BaselineToleranceFactor;
            bool sameBaseline = Math.Abs(chunk.BBox.BottomY - currentBaseline) <= tolerance;

            // Check column boundary
            bool sameColumn = true;
            if (columnLayout != null && currentLineChunks.Count > 0)
            {
                sameColumn = GetColumnIndex(currentLineChunks[0].BBox.CenterX, columnLayout) ==
                             GetColumnIndex(chunk.BBox.CenterX, columnLayout);
            }

            if (sameBaseline && sameColumn)
            {
                currentLineChunks.Add(chunk);
            }
            else
            {
                lines.Add(BuildLine(currentLineChunks));
                currentLineChunks = new List<TextChunk> { chunk };
                currentBaseline = chunk.BBox.BottomY;
            }
        }

        if (currentLineChunks.Count > 0)
        {
            lines.Add(BuildLine(currentLineChunks));
        }

        return lines;
    }

    private static TextLine BuildLine(List<TextChunk> chunks)
    {
        // Sort chunks left-to-right within the line
        chunks.Sort((a, b) => a.BBox.LeftX.CompareTo(b.BBox.LeftX));

        var bbox = chunks[0].BBox;
        foreach (var chunk in chunks.Skip(1))
        {
            bbox = bbox.Union(chunk.BBox);
        }

        return new TextLine
        {
            BBox = bbox,
            Chunks = chunks
        };
    }

    private static int GetColumnIndex(double x, ColumnLayout layout)
    {
        for (int i = 0; i < layout.Columns.Count; i++)
        {
            if (x >= layout.Columns[i].LeftX && x <= layout.Columns[i].RightX)
                return i;
        }
        return 0;
    }
}
