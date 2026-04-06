using EdgeParse.Core.Models;
using EdgeParse.Core.Pipeline.Stages;
using Xunit;

namespace EdgeParse.Tests;

public class ListDetectorTests
{
    [Fact]
    public void DetectLists_BulletItems_CreatesUnorderedList()
    {
        var elements = new List<ContentElement>
        {
            CreateTextElement("• First item"),
            CreateTextElement("• Second item"),
            CreateTextElement("• Third item"),
        };

        var result = ListDetector.DetectLists(elements);

        Assert.Single(result);
        Assert.Equal(ContentElementType.List, result[0].Type);
        Assert.Equal(3, result[0].List!.Items.Count);
        Assert.Equal(ListType.Unordered, result[0].List!.ListTypeValue);
    }

    [Fact]
    public void DetectLists_NumberedItems_CreatesOrderedList()
    {
        var elements = new List<ContentElement>
        {
            CreateTextElement("1. First item"),
            CreateTextElement("2. Second item"),
            CreateTextElement("3. Third item"),
        };

        var result = ListDetector.DetectLists(elements);

        Assert.Single(result);
        Assert.Equal(ContentElementType.List, result[0].Type);
        Assert.Equal(ListType.Ordered, result[0].List!.ListTypeValue);
    }

    [Fact]
    public void DetectLists_BracketNumbers_CreatesBracketList()
    {
        var elements = new List<ContentElement>
        {
            CreateTextElement("[1] First reference"),
            CreateTextElement("[2] Second reference"),
        };

        var result = ListDetector.DetectLists(elements);

        Assert.Single(result);
        Assert.Equal(ListType.BracketNumber, result[0].List!.ListTypeValue);
    }

    [Fact]
    public void DetectLists_SingleItem_NotAList()
    {
        var elements = new List<ContentElement>
        {
            CreateTextElement("• Only one item"),
        };

        var result = ListDetector.DetectLists(elements);

        // Single item is not enough for a list
        Assert.Single(result);
        Assert.NotEqual(ContentElementType.List, result[0].Type);
    }

    [Fact]
    public void DetectLists_MixedContent_PreservesNonListItems()
    {
        var elements = new List<ContentElement>
        {
            CreateTextElement("Introduction paragraph"),
            CreateTextElement("• First item"),
            CreateTextElement("• Second item"),
            CreateTextElement("Conclusion paragraph"),
        };

        var result = ListDetector.DetectLists(elements);

        Assert.Equal(3, result.Count);
        Assert.Equal(ContentElementType.Paragraph, result[0].Type);
        Assert.Equal(ContentElementType.List, result[1].Type);
        Assert.Equal(ContentElementType.Paragraph, result[2].Type);
    }

    private static ContentElement CreateTextElement(string text)
    {
        var block = new TextBlock
        {
            BBox = new BoundingBox(1, 72, 700, 500, 712),
            Lines = new List<TextLine>
            {
                new()
                {
                    BBox = new BoundingBox(1, 72, 700, 500, 712),
                    Chunks = new List<TextChunk>
                    {
                        new() { Value = text, BBox = new BoundingBox(1, 72, 700, 500, 712), FontSize = 12 }
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
