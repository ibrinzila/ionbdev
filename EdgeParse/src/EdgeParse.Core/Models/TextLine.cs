namespace EdgeParse.Core.Models;

/// <summary>
/// A line of text formed by grouping TextChunks sharing the same baseline.
/// </summary>
public class TextLine
{
    public BoundingBox BBox { get; set; } = new();
    public List<TextChunk> Chunks { get; set; } = new();
    public int? Index { get; set; }

    public string Value()
    {
        if (Chunks.Count == 0) return string.Empty;

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < Chunks.Count; i++)
        {
            if (i > 0 && NeedsSpace(Chunks[i - 1], Chunks[i]))
            {
                sb.Append(' ');
            }
            sb.Append(Chunks[i].Value);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Determines if a space is needed between two adjacent chunks.
    /// Gap > font_size * 0.17 indicates a word boundary.
    /// </summary>
    private static bool NeedsSpace(TextChunk prev, TextChunk current)
    {
        if (prev.Value.EndsWith(' ') || current.Value.StartsWith(' '))
            return false;

        double gap = current.BBox.LeftX - prev.BBox.RightX;
        double threshold = prev.FontSize * 0.17;
        return gap > threshold;
    }

    public double FontSize => Chunks.Count > 0 ? Chunks.Max(c => c.FontSize) : 0;
    public double FontWeight => Chunks.Count > 0 ? Chunks.Max(c => c.FontWeight) : 400;
    public string FontName => Chunks.Count > 0 ? Chunks[0].FontName : string.Empty;
    public bool IsItalic => Chunks.Count > 0 && Chunks.Any(c => c.IsItalic);
}
