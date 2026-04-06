using EdgeParse.Core.Models;
using EdgeParse.Core.Utils;
using Xunit;

namespace EdgeParse.Tests;

public class XyCutTests
{
    [Fact]
    public void Sort_SingleElement_ReturnsSame()
    {
        var elements = new List<ContentElement>
        {
            CreateParagraph(1, 10, 700, 300, 720)
        };

        var sorted = XyCut.Sort(elements);
        Assert.Single(sorted);
    }

    [Fact]
    public void Sort_TwoElements_TopToBottom()
    {
        var elements = new List<ContentElement>
        {
            CreateParagraph(1, 72, 100, 500, 120), // bottom
            CreateParagraph(1, 72, 700, 500, 720)  // top
        };

        var sorted = XyCut.Sort(elements);
        // Top element should come first (higher Y = earlier in reading order)
        Assert.Equal(720, sorted[0].BBox.TopY);
        Assert.Equal(120, sorted[1].BBox.TopY);
    }

    [Fact]
    public void Sort_TwoColumns_LeftBeforeRight()
    {
        var elements = new List<ContentElement>
        {
            // Right column
            CreateParagraph(1, 320, 700, 550, 720),
            CreateParagraph(1, 320, 670, 550, 690),
            // Left column
            CreateParagraph(1, 72, 700, 280, 720),
            CreateParagraph(1, 72, 670, 280, 690),
        };

        var sorted = XyCut.Sort(elements);

        // Left column should come first
        Assert.True(sorted[0].BBox.LeftX < 300);
        Assert.True(sorted[1].BBox.LeftX < 300);
        Assert.True(sorted[2].BBox.LeftX > 300);
        Assert.True(sorted[3].BBox.LeftX > 300);
    }

    [Fact]
    public void Sort_MultiplePages_PreservesPageOrder()
    {
        var elements = new List<ContentElement>
        {
            CreateParagraph(2, 72, 700, 500, 720),
            CreateParagraph(1, 72, 700, 500, 720),
        };

        var sorted = XyCut.Sort(elements);
        Assert.Equal(1, sorted[0].PageNumber);
        Assert.Equal(2, sorted[1].PageNumber);
    }

    [Fact]
    public void Sort_EmptyList_ReturnsEmpty()
    {
        var sorted = XyCut.Sort(new List<ContentElement>());
        Assert.Empty(sorted);
    }

    private static ContentElement CreateParagraph(int page, double left, double bottom, double right, double top)
    {
        var block = new TextBlock
        {
            BBox = new BoundingBox(page, left, bottom, right, top),
            Lines = new List<TextLine>
            {
                new()
                {
                    BBox = new BoundingBox(page, left, bottom, right, top),
                    Chunks = new List<TextChunk>
                    {
                        new()
                        {
                            Value = "Test text",
                            BBox = new BoundingBox(page, left, bottom, right, top),
                            FontSize = 12,
                            FontWeight = 400
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
                FontSize = 12,
                FontWeight = 400,
                Columns = new List<TextColumn>
                {
                    new() { TextBlocks = new List<TextBlock> { block } }
                }
            }
        };

        return ContentElement.FromParagraph(para);
    }
}
