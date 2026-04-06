namespace EdgeParse.Core.Pipeline.Stages;

using EdgeParse.Core.Models;

/// <summary>
/// Stage 10: Converts TextBlocks to SemanticParagraph elements.
/// </summary>
public static class ParagraphDetector
{
    public static List<ContentElement> ConvertToParagraphs(List<ContentElement> elements)
    {
        var result = new List<ContentElement>();

        foreach (var elem in elements)
        {
            if (elem.Type == ContentElementType.TextBlock)
            {
                var block = elem.TextBlock!;
                var paragraph = CreateParagraphFromBlock(block);
                result.Add(ContentElement.FromParagraph(paragraph));
            }
            else
            {
                result.Add(elem);
            }
        }

        return result;
    }

    public static SemanticParagraph CreateParagraphFromBlock(TextBlock block)
    {
        return new SemanticParagraph
        {
            Base = new SemanticTextNode
            {
                BBox = block.BBox,
                Index = block.Index,
                SemanticType = SemanticType.Paragraph,
                FontSize = block.FontSize,
                FontWeight = block.FontWeight,
                FontName = block.FontName,
                Columns = new List<TextColumn>
                {
                    new() { TextBlocks = new List<TextBlock> { block } }
                }
            }
        };
    }
}
