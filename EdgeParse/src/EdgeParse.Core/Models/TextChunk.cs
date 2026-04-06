namespace EdgeParse.Core.Models;

/// <summary>
/// A raw text chunk extracted from PDF content streams.
/// Contains decoded Unicode text with font metrics and positioning.
/// </summary>
public class TextChunk
{
    public string Value { get; set; } = string.Empty;
    public BoundingBox BBox { get; set; } = new();
    public string FontName { get; set; } = string.Empty;
    public double FontSize { get; set; }
    public double FontWeight { get; set; } = 400.0;
    public string FontColor { get; set; } = "#000000";
    public double ContrastRatio { get; set; } = 21.0;
    public List<double> SymbolEnds { get; set; } = new();
    public TextFormat TextFormat { get; set; } = TextFormat.Normal;
    public TextType TextType { get; set; } = TextType.Regular;
    public bool OcgVisible { get; set; } = true;
    public int? Mcid { get; set; }
    public bool IsItalic { get; set; }
    public double ItalicAngle { get; set; }
}

/// <summary>
/// An image chunk extracted from the PDF.
/// </summary>
public class ImageChunk
{
    public BoundingBox BBox { get; set; } = new();
    public int? Index { get; set; }
    public byte[]? ImageData { get; set; }
    public string? MimeType { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

/// <summary>
/// A line (horizontal or vertical rule) extracted from the PDF.
/// Used for table border detection.
/// </summary>
public class LineChunk
{
    public BoundingBox BBox { get; set; } = new();
    public double StartX { get; set; }
    public double StartY { get; set; }
    public double EndX { get; set; }
    public double EndY { get; set; }
    public double LineWidth { get; set; }
    public bool IsHorizontal => Math.Abs(StartY - EndY) < 1.0;
    public bool IsVertical => Math.Abs(StartX - EndX) < 1.0;
}

/// <summary>
/// Complex vector graphics (paths, curves) extracted from the PDF.
/// </summary>
public class LineArtChunk
{
    public BoundingBox BBox { get; set; } = new();
    public int? Index { get; set; }
}
