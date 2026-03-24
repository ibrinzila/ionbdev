namespace EdgeParse.Core.Pipeline.Stages;

using EdgeParse.Core.Models;
using System.Text.RegularExpressions;

/// <summary>
/// Stage 14: Links captions to figures/tables via proximity heuristics.
/// </summary>
public static class CaptionLinker
{
    private static readonly Regex CaptionPrefixRegex = new(
        @"^(Figure|Fig\.|Table|Tab\.|Image|Plate|Diagram|Chart|Graph|Illustration|Map|Photo)\s*\.?\s*\d*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private const double MaxCaptionDistance = 30.0; // pt

    public static List<ContentElement> LinkCaptions(List<ContentElement> elements)
    {
        var result = new List<ContentElement>(elements);

        for (int i = 0; i < result.Count; i++)
        {
            var elem = result[i];
            string? text = elem.TextValue?.Trim();
            if (text == null) continue;

            // Check if this looks like a caption
            if (!CaptionPrefixRegex.IsMatch(text)) continue;

            // Look for nearby figure or table
            int? linkedIndex = FindNearestFigureOrTable(result, i);

            if (linkedIndex != null)
            {
                var caption = new SemanticCaption
                {
                    Base = elem.Type == ContentElementType.Paragraph
                        ? elem.Paragraph!
                        : new SemanticParagraph
                        {
                            Base = new SemanticTextNode
                            {
                                BBox = elem.BBox,
                                SemanticType = SemanticType.Caption,
                                Columns = new List<TextColumn>
                                {
                                    new()
                                    {
                                        TextBlocks = new List<TextBlock>
                                        {
                                            new()
                                            {
                                                BBox = elem.BBox,
                                                Lines = new List<TextLine>
                                                {
                                                    new()
                                                    {
                                                        BBox = elem.BBox,
                                                        Chunks = new List<TextChunk>
                                                        {
                                                            new() { Value = text, BBox = elem.BBox }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        },
                    LinkedElementIndex = result[linkedIndex.Value].Index
                };
                caption.Base.Base.SemanticType = SemanticType.Caption;

                result[i] = ContentElement.FromCaption(caption);

                // Link the caption to the figure
                if (result[linkedIndex.Value].Type == ContentElementType.Figure)
                {
                    result[linkedIndex.Value].Figure!.Caption = caption;
                }
                else if (result[linkedIndex.Value].Type == ContentElementType.Table)
                {
                    result[linkedIndex.Value].Table!.Caption = caption;
                }
            }
        }

        return result;
    }

    private static int? FindNearestFigureOrTable(List<ContentElement> elements, int captionIndex)
    {
        var caption = elements[captionIndex];
        double minDist = double.MaxValue;
        int? bestIndex = null;

        // Search nearby elements (within a few positions)
        int searchRange = Math.Min(5, elements.Count);

        for (int offset = 1; offset <= searchRange; offset++)
        {
            foreach (int dir in new[] { -1, 1 })
            {
                int idx = captionIndex + (offset * dir);
                if (idx < 0 || idx >= elements.Count) continue;

                var elem = elements[idx];
                if (elem.Type != ContentElementType.Figure && elem.Type != ContentElementType.Table)
                    continue;

                if (elem.PageNumber != caption.PageNumber) continue;

                double dist = VerticalDistance(caption.BBox, elem.BBox);
                if (dist < MaxCaptionDistance && dist < minDist)
                {
                    minDist = dist;
                    bestIndex = idx;
                }
            }
        }

        return bestIndex;
    }

    private static double VerticalDistance(BoundingBox a, BoundingBox b)
    {
        if (a.TopY < b.BottomY)
            return b.BottomY - a.TopY;
        if (b.TopY < a.BottomY)
            return a.BottomY - b.TopY;
        return 0; // overlapping
    }
}
