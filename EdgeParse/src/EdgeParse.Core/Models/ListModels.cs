namespace EdgeParse.Core.Models;

/// <summary>
/// A detected list in the document.
/// </summary>
public class PdfList
{
    public BoundingBox BBox { get; set; } = new();
    public int? Index { get; set; }
    public List<PdfListItem> Items { get; set; } = new();
    public ListType ListTypeValue { get; set; }
}

public class PdfListItem
{
    public ListLabel Label { get; set; } = new();
    public SemanticParagraph Body { get; set; } = new();
}

public class ListLabel
{
    public string Value { get; set; } = string.Empty;
    public ListType Type { get; set; }
}
