namespace EdgeParse.Core.Models;

/// <summary>
/// Base for semantic text elements (paragraphs, headings, captions, etc.)
/// </summary>
public class SemanticTextNode
{
    public BoundingBox BBox { get; set; } = new();
    public int? Index { get; set; }
    public string? Level { get; set; }
    public SemanticType SemanticType { get; set; }
    public double? CorrectSemanticScore { get; set; }
    public List<TextColumn> Columns { get; set; } = new();
    public double? FontWeight { get; set; }
    public double? FontSize { get; set; }
    public string? FontColor { get; set; }
    public double? ItalicAngle { get; set; }
    public string? FontName { get; set; }
    public TextFormat? TextFormatValue { get; set; }
    public double? MaxFontSize { get; set; }
    public bool IsHiddenText { get; set; }

    public string Value()
    {
        return string.Join("\n", Columns.Select(c => c.Value()));
    }

    public int LinesCount => Columns.Sum(c => c.TextBlocks.Sum(b => b.LineCount));
}

public class TextColumn
{
    public List<TextBlock> TextBlocks { get; set; } = new();

    public string Value()
    {
        return string.Join(" ", TextBlocks.Select(b => b.Value()));
    }
}

/// <summary>
/// A paragraph of text with semantic classification.
/// </summary>
public class SemanticParagraph
{
    public SemanticTextNode Base { get; set; } = new() { SemanticType = SemanticType.Paragraph };

    public BoundingBox BBox => Base.BBox;
    public int? Index { get => Base.Index; set => Base.Index = value; }
    public string Value() => Base.Value();
}

/// <summary>
/// A heading with assigned level (1-6).
/// </summary>
public class SemanticHeading
{
    public SemanticParagraph Base { get; set; } = new();
    public int? HeadingLevel { get; set; }

    public BoundingBox BBox => Base.BBox;
    public int? Index { get => Base.Index; set => Base.Index = value; }
    public string Value() => Base.Value();
}

/// <summary>
/// A numbered heading (e.g., "1.2.3 Section Title").
/// </summary>
public class SemanticNumberHeading
{
    public SemanticHeading Base { get; set; } = new();

    public BoundingBox BBox => Base.BBox;
    public int? Index { get => Base.Index; set => Base.Index = value; }
    public string Value() => Base.Value();
}

/// <summary>
/// A caption for a figure or table.
/// </summary>
public class SemanticCaption
{
    public SemanticParagraph Base { get; set; } = new();
    public int? LinkedElementIndex { get; set; }

    public BoundingBox BBox => Base.BBox;
    public int? Index { get => Base.Index; set => Base.Index = value; }
    public string Value() => Base.Value();
}

/// <summary>
/// A figure containing images and/or line art.
/// </summary>
public class SemanticFigure
{
    public BoundingBox BBox { get; set; } = new();
    public int? Index { get; set; }
    public List<ImageChunk> Images { get; set; } = new();
    public List<LineArtChunk> LineArts { get; set; } = new();
    public SemanticCaption? Caption { get; set; }
}

/// <summary>
/// A header or footer detected across pages.
/// </summary>
public class SemanticHeaderOrFooter
{
    public SemanticParagraph Base { get; set; } = new();
    public bool IsHeader { get; set; }

    public BoundingBox BBox => Base.BBox;
    public int? Index { get => Base.Index; set => Base.Index = value; }
    public string Value() => Base.Value();
}

/// <summary>
/// A formula element.
/// </summary>
public class SemanticFormula
{
    public SemanticParagraph Base { get; set; } = new();

    public BoundingBox BBox => Base.BBox;
    public int? Index { get => Base.Index; set => Base.Index = value; }
    public string Value() => Base.Value();
}
