namespace EdgeParse.Core.Pipeline.Stages;

using EdgeParse.Core.Models;
using System.Text.RegularExpressions;

/// <summary>
/// Stage 12: Detects headings based on font size/weight rarity and neighbor comparison.
/// Assigns heading levels 1-6 based on visual appearance grouping.
/// </summary>
public static class HeadingDetector
{
    private const double HeadingScoreThreshold = 0.75;
    private const int MaxHeadingLines = 4;
    private const int MaxHeadingLength = 150;

    /// <summary>
    /// Detects headings among paragraphs and promotes them to SemanticHeading.
    /// Returns the updated list of content elements.
    /// </summary>
    public static List<ContentElement> DetectHeadings(List<ContentElement> elements)
    {
        // Collect font statistics from all paragraphs
        var paragraphs = elements
            .Where(e => e.Type == ContentElementType.Paragraph)
            .ToList();

        if (paragraphs.Count < 2) return elements;

        var fontSizeDistribution = new Dictionary<double, int>();
        var fontWeightDistribution = new Dictionary<double, int>();

        foreach (var p in paragraphs)
        {
            var fontSize = p.Paragraph!.Base.FontSize ?? 0;
            var fontWeight = p.Paragraph.Base.FontWeight ?? 400;

            var roundedSize = Math.Round(fontSize, 1);
            fontSizeDistribution[roundedSize] = fontSizeDistribution.GetValueOrDefault(roundedSize) + 1;
            fontWeightDistribution[fontWeight] = fontWeightDistribution.GetValueOrDefault(fontWeight) + 1;
        }

        int totalParagraphs = paragraphs.Count;

        // Score each paragraph
        var headingCandidates = new List<(int Index, ContentElement Element, double Score, double FontSize, double FontWeight)>();

        for (int i = 0; i < elements.Count; i++)
        {
            var elem = elements[i];
            if (elem.Type != ContentElementType.Paragraph) continue;

            var para = elem.Paragraph!;
            var text = para.Value();
            var fontSize = para.Base.FontSize ?? 0;
            var fontWeight = para.Base.FontWeight ?? 400;
            var lineCount = para.Base.LinesCount;

            // Skip if too long or too many lines
            if (text.Length > MaxHeadingLength) continue;
            if (lineCount > MaxHeadingLines) continue;
            if (text.Length < 1) continue;

            // Skip false positive patterns
            if (IsHeadingFalsePositive(text)) continue;

            // Calculate score
            double score = CalculateHeadingScore(
                fontSize, fontWeight,
                fontSizeDistribution, fontWeightDistribution,
                totalParagraphs, lineCount,
                elements, i);

            if (score > HeadingScoreThreshold)
            {
                headingCandidates.Add((i, elem, score, fontSize, fontWeight));
            }
        }

        if (headingCandidates.Count == 0) return elements;

        // Assign heading levels based on visual appearance grouping
        var levelMap = AssignHeadingLevels(headingCandidates);

        // Replace paragraphs with headings
        var result = new List<ContentElement>(elements);
        foreach (var candidate in headingCandidates)
        {
            var para = candidate.Element.Paragraph!;
            var heading = new SemanticHeading
            {
                Base = para,
                HeadingLevel = levelMap.GetValueOrDefault((candidate.FontSize, candidate.FontWeight), 1)
            };
            heading.Base.Base.SemanticType = SemanticType.Heading;

            result[candidate.Index] = ContentElement.FromHeading(heading);
        }

        return result;
    }

    private static double CalculateHeadingScore(
        double fontSize, double fontWeight,
        Dictionary<double, int> fontSizeDist, Dictionary<double, int> fontWeightDist,
        int totalParagraphs, int lineCount,
        List<ContentElement> elements, int currentIndex)
    {
        // Base size score: rarity of font size (rarer = more likely heading)
        double roundedSize = Math.Round(fontSize, 1);
        double sizeFrequency = fontSizeDist.GetValueOrDefault(roundedSize) / (double)totalParagraphs;
        double sizeScore = Math.Min(0.5, 1.0 - sizeFrequency);

        // Weight score: rarity of font weight
        double weightFrequency = fontWeightDist.GetValueOrDefault(fontWeight) / (double)totalParagraphs;
        double weightScore = Math.Min(0.3, 1.0 - weightFrequency);

        // Neighbor comparison score
        double neighborScore = CalculateNeighborScore(elements, currentIndex, fontSize, fontWeight);

        // Lines penalty: penalize multi-line text
        double linesPenalty = Math.Max(0, 1.0 - 0.05 * Math.Pow(lineCount - 1, 2));

        double finalScore = sizeScore * linesPenalty + weightScore + neighborScore;
        return Math.Min(1.0, finalScore);
    }

    private static double CalculateNeighborScore(
        List<ContentElement> elements, int currentIndex,
        double fontSize, double fontWeight)
    {
        double score = 0;

        // Compare with previous element
        var prev = FindNeighborParagraph(elements, currentIndex, -1);
        if (prev != null)
        {
            double prevSize = prev.Base.FontSize ?? 0;
            double prevWeight = prev.Base.FontWeight ?? 400;

            if (fontSize > prevSize + 0.5) score += 0.15;
            if (fontWeight > prevWeight + 100) score += 0.1;
        }

        // Compare with next element
        var next = FindNeighborParagraph(elements, currentIndex, 1);
        if (next != null)
        {
            double nextSize = next.Base.FontSize ?? 0;
            double nextWeight = next.Base.FontWeight ?? 400;

            if (fontSize > nextSize + 0.5) score += 0.15;
            if (fontWeight > nextWeight + 100) score += 0.1;
        }

        return Math.Min(0.5, score);
    }

    private static SemanticParagraph? FindNeighborParagraph(
        List<ContentElement> elements, int index, int direction)
    {
        int i = index + direction;
        while (i >= 0 && i < elements.Count)
        {
            if (elements[i].Type == ContentElementType.Paragraph)
                return elements[i].Paragraph;
            if (elements[i].Type == ContentElementType.Heading)
                return elements[i].Heading!.Base;
            i += direction;
        }
        return null;
    }

    private static bool IsHeadingFalsePositive(string text)
    {
        text = text.Trim();

        // Ends with comma (continuation)
        if (text.EndsWith(',')) return true;

        // Ends with hyphen (word break)
        if (text.EndsWith('-') && !text.EndsWith(" -")) return true;

        // Contains sentence break pattern ". [A-Z]"
        if (Regex.IsMatch(text, @"\.\s+[A-Z]") && text.Length > 50) return true;

        // Single character
        if (text.Length <= 1) return true;

        return false;
    }

    private static Dictionary<(double FontSize, double FontWeight), int> AssignHeadingLevels(
        List<(int Index, ContentElement Element, double Score, double FontSize, double FontWeight)> candidates)
    {
        // Group by visual appearance
        var groups = candidates
            .GroupBy(c => (Math.Round(c.FontSize, 1), c.FontWeight))
            .OrderByDescending(g => g.Key.Item1) // Largest font first
            .ThenByDescending(g => g.Key.FontWeight) // Boldest first
            .ToList();

        var levelMap = new Dictionary<(double, double), int>();
        int level = 1;

        foreach (var group in groups)
        {
            levelMap[group.Key] = Math.Min(level, 6);
            level++;
        }

        return levelMap;
    }
}
