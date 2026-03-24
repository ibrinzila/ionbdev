namespace EdgeParse.Core.Models;

/// <summary>
/// Information about a single PDF page.
/// </summary>
public class PageInfo
{
    public int PageNumber { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double MediaBoxLeft { get; set; }
    public double MediaBoxBottom { get; set; }
    public double MediaBoxRight { get; set; }
    public double MediaBoxTop { get; set; }
    public double? CropBoxLeft { get; set; }
    public double? CropBoxBottom { get; set; }
    public double? CropBoxRight { get; set; }
    public double? CropBoxTop { get; set; }
    public int Rotation { get; set; }

    public double EffectiveLeft => CropBoxLeft ?? MediaBoxLeft;
    public double EffectiveBottom => CropBoxBottom ?? MediaBoxBottom;
    public double EffectiveRight => CropBoxRight ?? MediaBoxRight;
    public double EffectiveTop => CropBoxTop ?? MediaBoxTop;
}

/// <summary>
/// Extracted chunks from a single page.
/// </summary>
public class PageChunks
{
    public int PageNumber { get; set; }
    public PageInfo PageInfo { get; set; } = new();
    public List<TextChunk> TextChunks { get; set; } = new();
    public List<ImageChunk> ImageChunks { get; set; } = new();
    public List<LineChunk> LineChunks { get; set; } = new();
    public List<LineArtChunk> LineArtChunks { get; set; } = new();
}

/// <summary>
/// Column layout detected on a page.
/// </summary>
public class ColumnLayout
{
    public int PageNumber { get; set; }
    public List<ColumnBoundary> Columns { get; set; } = new();
    public int ColumnCount => Columns.Count;
}

public class ColumnBoundary
{
    public double LeftX { get; set; }
    public double RightX { get; set; }
}
