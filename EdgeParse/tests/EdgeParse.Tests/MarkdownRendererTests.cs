using EdgeParse.Core.Models;
using EdgeParse.Core.Output;
using Xunit;

namespace EdgeParse.Tests;

public class MarkdownRendererTests
{
    [Fact]
    public void Render_Heading_ProducesMarkdownHeading()
    {
        var doc = CreateDocWithElements(
            CreateHeading("Introduction", 1)
        );

        var result = MarkdownRenderer.Render(doc);
        Assert.Contains("# Introduction", result);
    }

    [Fact]
    public void Render_HeadingLevel2_ProducesDoubleHash()
    {
        var doc = CreateDocWithElements(
            CreateHeading("Methods", 2)
        );

        var result = MarkdownRenderer.Render(doc);
        Assert.Contains("## Methods", result);
    }

    [Fact]
    public void Render_Paragraph_ProducesPlainText()
    {
        var doc = CreateDocWithElements(
            CreateParagraphElement("This is a paragraph.")
        );

        var result = MarkdownRenderer.Render(doc);
        Assert.Contains("This is a paragraph.", result);
    }

    [Fact]
    public void Render_Table_ProducesMarkdownTable()
    {
        var table = new TableBorder
        {
            BBox = new BoundingBox(1, 72, 500, 500, 700),
            NumRows = 2,
            NumColumns = 2,
            Rows = new List<TableBorderRow>
            {
                new()
                {
                    RowNumber = 0,
                    Cells = new List<TableBorderCell>
                    {
                        new() { BBox = new BoundingBox(), Content = new List<string> { "Header 1" } },
                        new() { BBox = new BoundingBox(), Content = new List<string> { "Header 2" } }
                    }
                },
                new()
                {
                    RowNumber = 1,
                    Cells = new List<TableBorderCell>
                    {
                        new() { BBox = new BoundingBox(), Content = new List<string> { "Cell 1" } },
                        new() { BBox = new BoundingBox(), Content = new List<string> { "Cell 2" } }
                    }
                }
            }
        };

        var doc = CreateDocWithElements(
            ContentElement.FromTable(new SemanticTable { Table = table })
        );

        var result = MarkdownRenderer.Render(doc);
        Assert.Contains("| Header 1 |", result);
        Assert.Contains("| --- |", result);
        Assert.Contains("| Cell 1 |", result);
    }

    [Fact]
    public void Render_WithPageSeparator_InsertsBetweenPages()
    {
        var doc = new PdfDocument
        {
            FileName = "test.pdf",
            NumberOfPages = 2,
            Kids = new List<ContentElement>
            {
                CreateParagraphElement("Page 1 text", pageNumber: 1),
                CreateParagraphElement("Page 2 text", pageNumber: 2),
            }
        };

        var result = MarkdownRenderer.Render(doc, "---");
        Assert.Contains("---", result);
    }

    private static PdfDocument CreateDocWithElements(params ContentElement[] elements)
    {
        return new PdfDocument
        {
            FileName = "test.pdf",
            NumberOfPages = 1,
            Kids = elements.ToList()
        };
    }

    private static ContentElement CreateHeading(string text, int level)
    {
        var heading = new SemanticHeading
        {
            HeadingLevel = level,
            Base = new SemanticParagraph
            {
                Base = new SemanticTextNode
                {
                    BBox = new BoundingBox(1, 72, 700, 500, 720),
                    SemanticType = SemanticType.Heading,
                    Columns = new List<TextColumn>
                    {
                        new()
                        {
                            TextBlocks = new List<TextBlock>
                            {
                                new()
                                {
                                    BBox = new BoundingBox(1, 72, 700, 500, 720),
                                    Lines = new List<TextLine>
                                    {
                                        new()
                                        {
                                            BBox = new BoundingBox(1, 72, 700, 500, 720),
                                            Chunks = new List<TextChunk>
                                            {
                                                new() { Value = text, BBox = new BoundingBox(1, 72, 700, 500, 720) }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        return ContentElement.FromHeading(heading);
    }

    private static ContentElement CreateParagraphElement(string text, int pageNumber = 1)
    {
        var para = new SemanticParagraph
        {
            Base = new SemanticTextNode
            {
                BBox = new BoundingBox(pageNumber, 72, 700, 500, 712),
                SemanticType = SemanticType.Paragraph,
                Columns = new List<TextColumn>
                {
                    new()
                    {
                        TextBlocks = new List<TextBlock>
                        {
                            new()
                            {
                                BBox = new BoundingBox(pageNumber, 72, 700, 500, 712),
                                Lines = new List<TextLine>
                                {
                                    new()
                                    {
                                        BBox = new BoundingBox(pageNumber, 72, 700, 500, 712),
                                        Chunks = new List<TextChunk>
                                        {
                                            new() { Value = text, BBox = new BoundingBox(pageNumber, 72, 700, 500, 712) }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        return ContentElement.FromParagraph(para);
    }
}
