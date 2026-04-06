namespace EdgeParse.Core.Models;

/// <summary>
/// Unified content element representing any extractable item from a PDF.
/// Mirrors the Rust ContentElement enum pattern using a discriminated wrapper.
/// </summary>
public class ContentElement
{
    public ContentElementType Type { get; set; }

    // Only one of these will be populated based on Type
    public TextChunk? TextChunk { get; set; }
    public TextLine? TextLine { get; set; }
    public TextBlock? TextBlock { get; set; }
    public ImageChunk? Image { get; set; }
    public LineChunk? Line { get; set; }
    public LineArtChunk? LineArt { get; set; }
    public SemanticParagraph? Paragraph { get; set; }
    public SemanticHeading? Heading { get; set; }
    public SemanticNumberHeading? NumberHeading { get; set; }
    public SemanticTable? Table { get; set; }
    public PdfList? List { get; set; }
    public SemanticCaption? Caption { get; set; }
    public SemanticFigure? Figure { get; set; }
    public SemanticFormula? Formula { get; set; }
    public SemanticHeaderOrFooter? HeaderFooter { get; set; }
    public TableBorder? TableBorder { get; set; }

    public BoundingBox BBox => Type switch
    {
        ContentElementType.TextChunk => TextChunk!.BBox,
        ContentElementType.TextLine => TextLine!.BBox,
        ContentElementType.TextBlock => TextBlock!.BBox,
        ContentElementType.Image => Image!.BBox,
        ContentElementType.Line => Line!.BBox,
        ContentElementType.LineArt => LineArt!.BBox,
        ContentElementType.Paragraph => Paragraph!.BBox,
        ContentElementType.Heading => Heading!.BBox,
        ContentElementType.NumberHeading => NumberHeading!.BBox,
        ContentElementType.Table => Table!.BBox,
        ContentElementType.List => List!.BBox,
        ContentElementType.Caption => Caption!.BBox,
        ContentElementType.Figure => Figure!.BBox,
        ContentElementType.Formula => Formula!.BBox,
        ContentElementType.HeaderFooter => HeaderFooter!.BBox,
        ContentElementType.TableBorder => TableBorder!.BBox,
        _ => new BoundingBox()
    };

    public int? Index => Type switch
    {
        ContentElementType.TextChunk => null,
        ContentElementType.TextLine => TextLine!.Index,
        ContentElementType.TextBlock => TextBlock!.Index,
        ContentElementType.Image => Image!.Index,
        ContentElementType.Line => null,
        ContentElementType.LineArt => LineArt!.Index,
        ContentElementType.Paragraph => Paragraph!.Index,
        ContentElementType.Heading => Heading!.Index,
        ContentElementType.NumberHeading => NumberHeading!.Index,
        ContentElementType.Table => Table!.Index,
        ContentElementType.List => List!.Index,
        ContentElementType.Caption => Caption!.Index,
        ContentElementType.Figure => Figure!.Index,
        ContentElementType.Formula => Formula!.Index,
        ContentElementType.HeaderFooter => HeaderFooter!.Index,
        ContentElementType.TableBorder => TableBorder!.Index,
        _ => null
    };

    public int PageNumber => BBox.PageNumber;

    public string? TextValue => Type switch
    {
        ContentElementType.TextChunk => TextChunk!.Value,
        ContentElementType.TextLine => TextLine!.Value(),
        ContentElementType.TextBlock => TextBlock!.Value(),
        ContentElementType.Paragraph => Paragraph!.Value(),
        ContentElementType.Heading => Heading!.Value(),
        ContentElementType.NumberHeading => NumberHeading!.Value(),
        ContentElementType.Caption => Caption!.Value(),
        ContentElementType.HeaderFooter => HeaderFooter!.Value(),
        ContentElementType.Formula => Formula!.Value(),
        _ => null
    };

    // Factory methods
    public static ContentElement FromTextChunk(TextChunk chunk) =>
        new() { Type = ContentElementType.TextChunk, TextChunk = chunk };

    public static ContentElement FromTextLine(TextLine line) =>
        new() { Type = ContentElementType.TextLine, TextLine = line };

    public static ContentElement FromTextBlock(TextBlock block) =>
        new() { Type = ContentElementType.TextBlock, TextBlock = block };

    public static ContentElement FromImage(ImageChunk image) =>
        new() { Type = ContentElementType.Image, Image = image };

    public static ContentElement FromLine(LineChunk line) =>
        new() { Type = ContentElementType.Line, Line = line };

    public static ContentElement FromLineArt(LineArtChunk lineArt) =>
        new() { Type = ContentElementType.LineArt, LineArt = lineArt };

    public static ContentElement FromParagraph(SemanticParagraph para) =>
        new() { Type = ContentElementType.Paragraph, Paragraph = para };

    public static ContentElement FromHeading(SemanticHeading heading) =>
        new() { Type = ContentElementType.Heading, Heading = heading };

    public static ContentElement FromNumberHeading(SemanticNumberHeading heading) =>
        new() { Type = ContentElementType.NumberHeading, NumberHeading = heading };

    public static ContentElement FromTable(SemanticTable table) =>
        new() { Type = ContentElementType.Table, Table = table };

    public static ContentElement FromList(PdfList list) =>
        new() { Type = ContentElementType.List, List = list };

    public static ContentElement FromCaption(SemanticCaption caption) =>
        new() { Type = ContentElementType.Caption, Caption = caption };

    public static ContentElement FromFigure(SemanticFigure figure) =>
        new() { Type = ContentElementType.Figure, Figure = figure };

    public static ContentElement FromFormula(SemanticFormula formula) =>
        new() { Type = ContentElementType.Formula, Formula = formula };

    public static ContentElement FromHeaderFooter(SemanticHeaderOrFooter hf) =>
        new() { Type = ContentElementType.HeaderFooter, HeaderFooter = hf };

    public static ContentElement FromTableBorder(TableBorder border) =>
        new() { Type = ContentElementType.TableBorder, TableBorder = border };
}

public enum ContentElementType
{
    TextChunk,
    TextLine,
    TextBlock,
    Image,
    Line,
    LineArt,
    Paragraph,
    Heading,
    NumberHeading,
    Table,
    List,
    Caption,
    Figure,
    Formula,
    HeaderFooter,
    TableBorder
}
