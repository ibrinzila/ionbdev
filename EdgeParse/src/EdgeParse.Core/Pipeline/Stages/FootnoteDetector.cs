namespace EdgeParse.Core.Pipeline.Stages;

using EdgeParse.Core.Models;
using System.Text.RegularExpressions;

/// <summary>
/// Stage 14b: Detects footnotes by finding superscript references in body text
/// and matching footnote bodies at the bottom of pages.
/// </summary>
public static class FootnoteDetector
{
    private static readonly Regex FootnoteBodyRegex = new(
        @"^\s*(\d{1,3})\s+\S", RegexOptions.Compiled);

    private static readonly Regex SuperscriptRefRegex = new(
        @"\d{1,3}$", RegexOptions.Compiled);

    private const double FootnoteZoneRatio = 0.2; // Bottom 20% of page

    public static List<ContentElement> DetectFootnotes(
        List<ContentElement> elements, Dictionary<int, PageInfo> pageInfos)
    {
        if (elements.Count < 3) return elements;

        var result = new List<ContentElement>(elements.Count);

        // Group elements by page
        var byPage = elements
            .Select((e, idx) => (Element: e, Index: idx))
            .GroupBy(x => x.Element.PageNumber)
            .ToDictionary(g => g.Key, g => g.ToList());

        var footnoteIndices = new HashSet<int>();

        foreach (var (pageNum, pageElements) in byPage)
        {
            if (!pageInfos.TryGetValue(pageNum, out var pageInfo)) continue;

            double footnoteThreshold = pageInfo.EffectiveBottom +
                (pageInfo.EffectiveTop - pageInfo.EffectiveBottom) * FootnoteZoneRatio;

            // Find small-font text at bottom of page that starts with a number
            foreach (var (elem, idx) in pageElements)
            {
                if (elem.BBox.TopY > footnoteThreshold) continue;

                var text = elem.TextValue?.Trim();
                if (string.IsNullOrEmpty(text)) continue;

                // Check if it looks like a footnote body: starts with a number
                if (FootnoteBodyRegex.IsMatch(text))
                {
                    // Also check if font size is smaller than average for this page
                    bool isSmaller = false;
                    if (elem.Type == ContentElementType.Paragraph)
                    {
                        var fontSize = elem.Paragraph!.Base.FontSize ?? 0;
                        var otherSizes = pageElements
                            .Where(x => x.Element.Type == ContentElementType.Paragraph &&
                                        x.Element.BBox.TopY > footnoteThreshold)
                            .Select(x => x.Element.Paragraph!.Base.FontSize ?? 0)
                            .Where(s => s > 0)
                            .ToList();

                        if (otherSizes.Count > 0)
                        {
                            double avgSize = otherSizes.Average();
                            isSmaller = fontSize < avgSize * 0.95;
                        }
                    }

                    if (isSmaller)
                    {
                        footnoteIndices.Add(idx);
                    }
                }
            }
        }

        // Replace detected footnotes
        for (int i = 0; i < elements.Count; i++)
        {
            if (footnoteIndices.Contains(i))
            {
                var elem = elements[i];
                var text = elem.TextValue ?? string.Empty;

                var hf = new SemanticHeaderOrFooter
                {
                    IsHeader = false,
                    Base = new SemanticParagraph
                    {
                        Base = new SemanticTextNode
                        {
                            BBox = elem.BBox,
                            SemanticType = SemanticType.Footnote,
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
                    }
                };
                result.Add(ContentElement.FromHeaderFooter(hf));
            }
            else
            {
                result.Add(elements[i]);
            }
        }

        return result;
    }
}
