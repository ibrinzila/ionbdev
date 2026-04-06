using EdgeParse.Core.Models;
using Xunit;

namespace EdgeParse.Tests;

public class TextLineTests
{
    [Fact]
    public void Value_SingleChunk_ReturnsText()
    {
        var line = new TextLine
        {
            BBox = new BoundingBox(1, 0, 0, 100, 12),
            Chunks = new List<TextChunk>
            {
                new() { Value = "Hello", BBox = new BoundingBox(1, 0, 0, 30, 12), FontSize = 12 }
            }
        };

        Assert.Equal("Hello", line.Value());
    }

    [Fact]
    public void Value_MultipleChunks_InsertsSpaces()
    {
        var line = new TextLine
        {
            BBox = new BoundingBox(1, 0, 0, 100, 12),
            Chunks = new List<TextChunk>
            {
                new() { Value = "Hello", BBox = new BoundingBox(1, 0, 0, 30, 12), FontSize = 12 },
                new() { Value = "World", BBox = new BoundingBox(1, 35, 0, 65, 12), FontSize = 12 }
            }
        };

        // Gap is 5pt, threshold is 12*0.17=2.04, so space should be inserted
        Assert.Equal("Hello World", line.Value());
    }

    [Fact]
    public void Value_AdjacentChunks_NoExtraSpace()
    {
        var line = new TextLine
        {
            BBox = new BoundingBox(1, 0, 0, 60, 12),
            Chunks = new List<TextChunk>
            {
                new() { Value = "Hel", BBox = new BoundingBox(1, 0, 0, 18, 12), FontSize = 12 },
                new() { Value = "lo", BBox = new BoundingBox(1, 18, 0, 30, 12), FontSize = 12 }
            }
        };

        // Gap is 0pt, no space needed
        Assert.Equal("Hello", line.Value());
    }

    [Fact]
    public void FontSize_ReturnsMaxChunkSize()
    {
        var line = new TextLine
        {
            Chunks = new List<TextChunk>
            {
                new() { FontSize = 12 },
                new() { FontSize = 14 },
                new() { FontSize = 10 }
            }
        };

        Assert.Equal(14, line.FontSize);
    }
}
