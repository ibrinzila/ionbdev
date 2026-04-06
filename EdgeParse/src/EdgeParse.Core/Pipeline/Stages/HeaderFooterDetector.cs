namespace EdgeParse.Core.Pipeline.Stages;

using EdgeParse.Core.Models;

/// <summary>
/// Stage 8: Detects headers and footers by finding content repeated in similar positions across pages.
/// </summary>
public static class HeaderFooterDetector
{
    private const double PositionTolerance = 5.0; // pt
    private const double MinPageRatio = 0.5; // Must appear on at least 50% of pages
    private const double HeaderZoneRatio = 0.1; // Top 10% of page
    private const double FooterZoneRatio = 0.1; // Bottom 10% of page

    /// <summary>
    /// Detects and marks header/footer elements.
    /// Returns elements with headers/footers replaced by SemanticHeaderOrFooter.
    /// </summary>
    public static List<ContentElement> DetectHeaderFooters(
        List<ContentElement> elements, Dictionary<int, PageInfo> pageInfos)
    {
        if (pageInfos.Count < 3) return elements; // Need multiple pages

        // Collect text elements with their positions
        var textElements = elements
            .Select((e, idx) => (Element: e, Index: idx))
            .Where(x => x.Element.TextValue != null)
            .ToList();

        // Group by text content
        var byText = new Dictionary<string, List<(ContentElement Element, int Index)>>();
        foreach (var (elem, idx) in textElements)
        {
            var text = elem.TextValue!.Trim();
            if (string.IsNullOrEmpty(text) || text.Length < 2) continue;

            if (!byText.ContainsKey(text))
                byText[text] = new List<(ContentElement, int)>();
            byText[text].Add((elem, idx));
        }

        double minPages = pageInfos.Count * MinPageRatio;
        var headerFooterIndices = new Dictionary<int, bool>(); // index -> isHeader

        foreach (var (text, occurrences) in byText)
        {
            if (occurrences.Count < minPages) continue;

            // Check distinct pages
            var distinctPages = occurrences.Select(o => o.Element.PageNumber).Distinct().Count();
            if (distinctPages < minPages) continue;

            // Check if positions are consistent
            var avgX = occurrences.Average(o => o.Element.BBox.CenterX);
            var avgY = occurrences.Average(o => o.Element.BBox.CenterY);

            bool consistentPosition = occurrences.All(o =>
                Math.Abs(o.Element.BBox.CenterX - avgX) < PositionTolerance);

            if (!consistentPosition) continue;

            // Determine if header or footer based on Y position
            bool isHeader = false;
            bool isFooter = false;

            foreach (var (elem, _) in occurrences)
            {
                if (!pageInfos.TryGetValue(elem.PageNumber, out var pageInfo)) continue;

                double relativeY = (elem.BBox.CenterY - pageInfo.EffectiveBottom) /
                                   (pageInfo.EffectiveTop - pageInfo.EffectiveBottom);

                if (relativeY > (1 - HeaderZoneRatio)) isHeader = true;
                if (relativeY < FooterZoneRatio) isFooter = true;
            }

            if (isHeader || isFooter)
            {
                foreach (var (_, idx) in occurrences)
                {
                    headerFooterIndices[idx] = isHeader;
                }
            }
        }

        // Replace detected header/footer elements
        var result = new List<ContentElement>(elements);
        foreach (var (idx, isHeader) in headerFooterIndices)
        {
            var elem = elements[idx];
            var text = elem.TextValue ?? string.Empty;

            var hf = new SemanticHeaderOrFooter
            {
                IsHeader = isHeader,
                Base = new SemanticParagraph
                {
                    Base = new SemanticTextNode
                    {
                        BBox = elem.BBox,
                        SemanticType = SemanticType.HeaderFooter,
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

            result[idx] = ContentElement.FromHeaderFooter(hf);
        }

        return result;
    }
}
