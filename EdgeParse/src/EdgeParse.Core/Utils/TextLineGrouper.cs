namespace EdgeParse.Core.Utils;

using EdgeParse.Core.Models;

/// <summary>
/// Groups TextChunks into TextLines based on shared baselines (Y-position tolerance).
/// Merges adjacent same-font character chunks into word-level chunks for efficiency.
/// Inserts spaces when gap > font_size * 0.17.
/// </summary>
public static class TextLineGrouper
{
    private const double BaselineToleranceFactor = 0.3;
    private const double SpaceGapFactor = 0.17;
    private const double MergeGapFactor = 0.05; // Adjacent chars in same word

    /// <summary>
    /// Groups text chunks on a page into text lines, merging character-level chunks
    /// into word-level chunks for performance.
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
        var currentLineChunks = new List<TextChunk>(64) { sorted[0] };
        double currentBaseline = sorted[0].BBox.BottomY;

        for (int i = 1; i < sorted.Count; i++)
        {
            var chunk = sorted[i];
            double tolerance = Math.Max(chunk.FontSize * BaselineToleranceFactor, 1.0);
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
                currentLineChunks = new List<TextChunk>(64) { chunk };
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

        // Merge adjacent character-level chunks into word-level chunks
        var mergedChunks = MergeAdjacentChunks(chunks);

        var bbox = mergedChunks[0].BBox;
        for (int i = 1; i < mergedChunks.Count; i++)
        {
            bbox = bbox.Union(mergedChunks[i].BBox);
        }

        return new TextLine
        {
            BBox = bbox,
            Chunks = mergedChunks
        };
    }

    /// <summary>
    /// Merges adjacent single-character chunks sharing the same font into multi-character chunks.
    /// This reduces chunk count by ~80% for typical PDFs and speeds up downstream processing.
    /// </summary>
    private static List<TextChunk> MergeAdjacentChunks(List<TextChunk> chunks)
    {
        if (chunks.Count <= 1) return chunks;

        var merged = new List<TextChunk>(chunks.Count / 3 + 1);
        var current = chunks[0];

        for (int i = 1; i < chunks.Count; i++)
        {
            var next = chunks[i];
            double gap = next.BBox.LeftX - current.BBox.RightX;
            double mergeThreshold = current.FontSize * MergeGapFactor;
            double spaceThreshold = current.FontSize * SpaceGapFactor;

            bool sameFont = current.FontName == next.FontName &&
                            Math.Abs(current.FontSize - next.FontSize) < 0.5 &&
                            Math.Abs(current.FontWeight - next.FontWeight) < 50;

            if (sameFont && gap <= spaceThreshold && gap >= -current.FontSize * 0.1)
            {
                // Merge: concatenate values, expand bounding box, collect symbol ends
                string separator = gap > mergeThreshold ? " " : "";
                current = new TextChunk
                {
                    Value = current.Value + separator + next.Value,
                    BBox = current.BBox.Union(next.BBox),
                    FontName = current.FontName,
                    FontSize = Math.Max(current.FontSize, next.FontSize),
                    FontWeight = Math.Max(current.FontWeight, next.FontWeight),
                    FontColor = current.FontColor,
                    ContrastRatio = Math.Min(current.ContrastRatio, next.ContrastRatio),
                    IsItalic = current.IsItalic || next.IsItalic,
                    TextFormat = current.TextFormat,
                    TextType = current.TextType,
                    OcgVisible = current.OcgVisible && next.OcgVisible,
                    Mcid = current.Mcid ?? next.Mcid,
                    SymbolEnds = MergeSymbolEnds(current.SymbolEnds, next.SymbolEnds)
                };
            }
            else
            {
                merged.Add(current);
                current = next;
            }
        }

        merged.Add(current);
        return merged;
    }

    private static List<double> MergeSymbolEnds(List<double> a, List<double> b)
    {
        var result = new List<double>(a.Count + b.Count);
        result.AddRange(a);
        result.AddRange(b);
        return result;
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
