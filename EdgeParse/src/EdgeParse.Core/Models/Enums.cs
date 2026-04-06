namespace EdgeParse.Core.Models;

public enum TextFormat
{
    Normal,
    Superscript,
    Subscript
}

public enum TextType
{
    Regular,
    Math,
    Code
}

public enum SemanticType
{
    Paragraph,
    Heading,
    NumberHeading,
    Table,
    List,
    Caption,
    Figure,
    Formula,
    Picture,
    HeaderFooter,
    Footnote,
    TableOfContents,
    Unknown
}

public enum ListType
{
    Ordered,
    Unordered,
    BracketNumber
}

public enum OutputFormat
{
    Json,
    Markdown,
    Html,
    Text
}

public enum ReadingOrder
{
    XyCut,
    Off
}

public enum TableMethod
{
    Default,
    Cluster
}

public enum ImageOutput
{
    Off,
    Embedded,
    External
}

public enum ImageFormat
{
    Png,
    Jpeg
}
