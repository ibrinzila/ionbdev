namespace EdgeParse.Core.Models;

/// <summary>
/// A block of text formed by grouping consecutive TextLines with similar formatting.
/// Represents a paragraph-level region before semantic classification.
/// </summary>
public class TextBlock
{
    public BoundingBox BBox { get; set; } = new();
    public List<TextLine> Lines { get; set; } = new();
    public int? Index { get; set; }
    public int? ColumnIndex { get; set; }

    public string Value()
    {
        return string.Join(" ", Lines.Select(l => l.Value()));
    }

    public int LineCount => Lines.Count;
    public double FontSize => Lines.Count > 0 ? Lines.Max(l => l.FontSize) : 0;
    public double FontWeight => Lines.Count > 0 ? Lines.Max(l => l.FontWeight) : 400;
    public string FontName => Lines.Count > 0 ? Lines[0].FontName : string.Empty;
    public bool IsItalic => Lines.Count > 0 && Lines.Any(l => l.IsItalic);

    /// <summary>
    /// Recomputes the bounding box from child lines.
    /// </summary>
    public void RecalculateBBox()
    {
        if (Lines.Count == 0) return;

        BBox = Lines[0].BBox;
        for (int i = 1; i < Lines.Count; i++)
        {
            BBox = BBox.Union(Lines[i].BBox);
        }
    }
}
