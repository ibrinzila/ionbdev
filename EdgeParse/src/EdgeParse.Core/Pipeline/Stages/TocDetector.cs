namespace EdgeParse.Core.Pipeline.Stages;

using EdgeParse.Core.Models;
using System.Text.RegularExpressions;

/// <summary>
/// Stage 14c: Detects Table of Contents sections.
/// Identifies TOC by patterns like "Title ........... 12" (dot leaders + page numbers).
/// </summary>
public static class TocDetector
{
    // Matches lines ending with dot leaders and page numbers
    private static readonly Regex DotLeaderRegex = new(
        @"\.{3,}\s*\d+\s*$", RegexOptions.Compiled);

    // Matches "Table of Contents" / "Contents" headings
    private static readonly Regex TocHeadingRegex = new(
        @"^(Table\s+of\s+Contents|Contents|CONTENTS|TABLE\s+OF\s+CONTENTS)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private const int MinTocEntries = 3;

    public static List<ContentElement> DetectTableOfContents(List<ContentElement> elements)
    {
        if (elements.Count < MinTocEntries) return elements;

        var result = new List<ContentElement>(elements.Count);
        int i = 0;

        while (i < elements.Count)
        {
            // Look for a TOC heading
            var text = elements[i].TextValue?.Trim() ?? "";

            if (TocHeadingRegex.IsMatch(text))
            {
                // Found a TOC heading - collect consecutive dot-leader entries
                result.Add(elements[i]);
                i++;

                int tocStart = result.Count;
                while (i < elements.Count)
                {
                    var entryText = elements[i].TextValue?.Trim() ?? "";
                    if (DotLeaderRegex.IsMatch(entryText) || IsLikelyTocEntry(entryText))
                    {
                        // Mark as TOC entry by wrapping in caption with TOC semantic
                        var caption = new SemanticCaption
                        {
                            Base = elements[i].Type == ContentElementType.Paragraph
                                ? elements[i].Paragraph!
                                : new SemanticParagraph
                                {
                                    Base = new SemanticTextNode
                                    {
                                        BBox = elements[i].BBox,
                                        SemanticType = SemanticType.TableOfContents,
                                        Columns = new List<TextColumn>
                                        {
                                            new()
                                            {
                                                TextBlocks = new List<TextBlock>
                                                {
                                                    new()
                                                    {
                                                        BBox = elements[i].BBox,
                                                        Lines = new List<TextLine>
                                                        {
                                                            new()
                                                            {
                                                                BBox = elements[i].BBox,
                                                                Chunks = new List<TextChunk>
                                                                {
                                                                    new() { Value = entryText, BBox = elements[i].BBox }
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
                        caption.Base.Base.SemanticType = SemanticType.TableOfContents;
                        result.Add(ContentElement.FromCaption(caption));
                        i++;
                    }
                    else
                    {
                        break; // End of TOC section
                    }
                }

                // If we didn't find enough TOC entries, the entries we marked
                // are still valid as captions
            }
            else
            {
                // Check for standalone dot-leader sequences (TOC without heading)
                int runStart = i;
                int dotLeaderCount = 0;
                while (i + dotLeaderCount < elements.Count)
                {
                    var entryText = elements[i + dotLeaderCount].TextValue?.Trim() ?? "";
                    if (DotLeaderRegex.IsMatch(entryText))
                        dotLeaderCount++;
                    else
                        break;
                }

                if (dotLeaderCount >= MinTocEntries)
                {
                    // Mark all as TOC entries
                    for (int j = 0; j < dotLeaderCount; j++)
                    {
                        var elem = elements[i + j];
                        var caption = new SemanticCaption
                        {
                            Base = elem.Type == ContentElementType.Paragraph
                                ? elem.Paragraph!
                                : new SemanticParagraph
                                {
                                    Base = new SemanticTextNode
                                    {
                                        BBox = elem.BBox,
                                        SemanticType = SemanticType.TableOfContents
                                    }
                                }
                        };
                        caption.Base.Base.SemanticType = SemanticType.TableOfContents;
                        result.Add(ContentElement.FromCaption(caption));
                    }
                    i += dotLeaderCount;
                }
                else
                {
                    result.Add(elements[i]);
                    i++;
                }
            }
        }

        return result;
    }

    private static bool IsLikelyTocEntry(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        // Short line ending with a page number
        if (text.Length < 100 && Regex.IsMatch(text, @"\d+\s*$") &&
            !text.Contains('.') && text.Contains("  "))
            return true;

        return false;
    }
}
