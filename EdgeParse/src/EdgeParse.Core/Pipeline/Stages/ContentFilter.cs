namespace EdgeParse.Core.Pipeline.Stages;

using EdgeParse.Core.Api;
using EdgeParse.Core.Models;

/// <summary>
/// Stage 2: Filters content based on FilterConfig settings.
/// Removes hidden text, out-of-page content, tiny text, and hidden OCG layers.
/// </summary>
public static class ContentFilter
{
    public static List<PageChunks> Filter(List<PageChunks> pages, FilterConfig config)
    {
        foreach (var page in pages)
        {
            if (config.FilterHiddenText)
            {
                page.TextChunks = page.TextChunks
                    .Where(c => c.ContrastRatio >= config.ContrastRatioThreshold)
                    .ToList();
            }

            if (config.FilterOutOfPage)
            {
                page.TextChunks = page.TextChunks
                    .Where(c => IsInsidePage(c.BBox, page.PageInfo))
                    .ToList();

                page.ImageChunks = page.ImageChunks
                    .Where(c => IsInsidePage(c.BBox, page.PageInfo))
                    .ToList();
            }

            if (config.FilterTinyText)
            {
                page.TextChunks = page.TextChunks
                    .Where(c => c.FontSize >= config.MinFontSize || c.BBox.Height >= config.MinFontSize)
                    .ToList();
            }

            if (config.FilterHiddenOcg)
            {
                page.TextChunks = page.TextChunks
                    .Where(c => c.OcgVisible)
                    .ToList();
            }

            // Replace U+FFFD with space
            foreach (var chunk in page.TextChunks)
            {
                chunk.Value = chunk.Value.Replace('\uFFFD', ' ');
            }
        }

        return pages;
    }

    private static bool IsInsidePage(BoundingBox bbox, PageInfo pageInfo)
    {
        // At least partially inside the effective page area
        return bbox.RightX > pageInfo.EffectiveLeft &&
               bbox.LeftX < pageInfo.EffectiveRight &&
               bbox.TopY > pageInfo.EffectiveBottom &&
               bbox.BottomY < pageInfo.EffectiveTop;
    }
}
