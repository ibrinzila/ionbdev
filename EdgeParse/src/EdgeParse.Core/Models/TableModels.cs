namespace EdgeParse.Core.Models;

/// <summary>
/// Represents a detected table with grid structure.
/// </summary>
public class TableBorder
{
    public BoundingBox BBox { get; set; } = new();
    public int? Index { get; set; }
    public List<double> XCoordinates { get; set; } = new();
    public List<double> YCoordinates { get; set; } = new();
    public List<TableBorderRow> Rows { get; set; } = new();
    public int NumRows { get; set; }
    public int NumColumns { get; set; }
    public bool IsBadTable { get; set; }
    public TableBorder? PreviousTable { get; set; }
    public TableBorder? NextTable { get; set; }
}

public class TableBorderRow
{
    public List<TableBorderCell> Cells { get; set; } = new();
    public int RowNumber { get; set; }
}

public class TableBorderCell
{
    public BoundingBox BBox { get; set; } = new();
    public int RowNumber { get; set; }
    public int ColNumber { get; set; }
    public int RowSpan { get; set; } = 1;
    public int ColSpan { get; set; } = 1;
    public List<string> Content { get; set; } = new();
    public List<ContentElement> Contents { get; set; } = new();

    public string TextContent => string.Join(" ", Content);
}

/// <summary>
/// A semantic table wrapping the border/grid structure.
/// </summary>
public class SemanticTable
{
    public TableBorder Table { get; set; } = new();
    public SemanticCaption? Caption { get; set; }

    public BoundingBox BBox => Table.BBox;
    public int? Index { get => Table.Index; set => Table.Index = value; }
}
