using EdgeParse.Core.Models;
using EdgeParse.Core.Pipeline.Stages;
using Xunit;

namespace EdgeParse.Tests;

public class HeadingDetectorTests
{
    [Fact]
    public void DetectHeadings_LargerFont_BecomesHeading()
    {
        var elements = new List<ContentElement>
        {
            CreateParagraph("Introduction", fontSize: 18, fontWeight: 700),
            CreateParagraph("This is body text paragraph one.", fontSize: 12, fontWeight: 400),
            CreateParagraph("This is body text paragraph two.", fontSize: 12, fontWeight: 400),
            CreateParagraph("This is body text paragraph three.", fontSize: 12, fontWeight: 400),
            CreateParagraph("Methods", fontSize: 18, fontWeight: 700),
            CreateParagraph("More body text here in this section.", fontSize: 12, fontWeight: 400),
        };

        var result = HeadingDetector.DetectHeadings(elements);

        // The large-font items should be promoted to headings
        Assert.Equal(ContentElementType.Heading, result[0].Type);
        Assert.Equal(ContentElementType.Paragraph, result[1].Type);
        Assert.Equal(ContentElementType.Heading, result[4].Type);
    }

    [Fact]
    public void DetectHeadings_SameFont_NoHeadings()
    {
        var elements = new List<ContentElement>
        {
            CreateParagraph("First paragraph text.", fontSize: 12, fontWeight: 400),
            CreateParagraph("Second paragraph text.", fontSize: 12, fontWeight: 400),
            CreateParagraph("Third paragraph text.", fontSize: 12, fontWeight: 400),
        };

        var result = HeadingDetector.DetectHeadings(elements);

        // No headings should be detected when all text is the same size
        Assert.All(result, e => Assert.Equal(ContentElementType.Paragraph, e.Type));
    }

    [Fact]
    public void DetectHeadings_SkipsFalsePositives_CommaEnding()
    {
        var elements = new List<ContentElement>
        {
            CreateParagraph("Not a heading,", fontSize: 18, fontWeight: 700),
            CreateParagraph("Body text continues.", fontSize: 12, fontWeight: 400),
            CreateParagraph("More body text here.", fontSize: 12, fontWeight: 400),
        };

        var result = HeadingDetector.DetectHeadings(elements);

        // Text ending with comma should not be a heading
        Assert.Equal(ContentElementType.Paragraph, result[0].Type);
    }

    private static ContentElement CreateParagraph(string text, double fontSize, double fontWeight)
    {
        var block = new TextBlock
        {
            BBox = new BoundingBox(1, 72, 700, 500, 700 + fontSize),
            Lines = new List<TextLine>
            {
                new()
                {
                    BBox = new BoundingBox(1, 72, 700, 500, 700 + fontSize),
                    Chunks = new List<TextChunk>
                    {
                        new()
                        {
                            Value = text,
                            BBox = new BoundingBox(1, 72, 700, 500, 700 + fontSize),
                            FontSize = fontSize,
                            FontWeight = fontWeight
                        }
                    }
                }
            }
        };

        var para = new SemanticParagraph
        {
            Base = new SemanticTextNode
            {
                BBox = block.BBox,
                SemanticType = SemanticType.Paragraph,
                FontSize = fontSize,
                FontWeight = fontWeight,
                Columns = new List<TextColumn>
                {
                    new() { TextBlocks = new List<TextBlock> { block } }
                }
            }
        };

        return ContentElement.FromParagraph(para);
    }
}
