namespace EdgeParse.Core.Pipeline.Stages;

using EdgeParse.Core.Models;
using System.Text.RegularExpressions;

/// <summary>
/// Stages 6.5, 9, 11: Detects lists in text content.
/// Handles bullet lists, numbered lists, and bracket-number lists.
/// </summary>
public static class ListDetector
{
    // Common bullet characters
    private static readonly HashSet<char> BulletChars = new()
    {
        '•', '◦', '▪', '▸', '►', '●', '○', '■', '□', '★', '☆',
        '-', '–', '—', '·', '∙', '⁃', '‣'
    };

    private static readonly Regex OrderedListRegex = new(@"^\s*(\d+)[.)]\s+", RegexOptions.Compiled);
    private static readonly Regex BracketNumberRegex = new(@"^\s*\[(\d+)\]\s*", RegexOptions.Compiled);
    private static readonly Regex LetterListRegex = new(@"^\s*([a-zA-Z])[.)]\s+", RegexOptions.Compiled);
    private static readonly Regex RomanListRegex = new(@"^\s*((?:i{1,3}|iv|vi{0,3}|ix|xi{0,3}|xiv|xv|xvi{0,3}))[.)]\s+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Detects lists in content elements and replaces sequences of list items
    /// with PdfList elements.
    /// </summary>
    public static List<ContentElement> DetectLists(List<ContentElement> elements)
    {
        var result = new List<ContentElement>();
        var currentListItems = new List<(ContentElement Element, ListLabel Label)>();
        ListType? currentListType = null;

        for (int i = 0; i < elements.Count; i++)
        {
            var elem = elements[i];
            string? text = elem.TextValue;

            if (text == null)
            {
                FlushList(result, currentListItems, currentListType);
                currentListItems.Clear();
                currentListType = null;
                result.Add(elem);
                continue;
            }

            var listMatch = TryMatchListItem(text);
            if (listMatch != null)
            {
                if (currentListType == null || currentListType == listMatch.Type)
                {
                    currentListItems.Add((elem, listMatch));
                    currentListType = listMatch.Type;
                }
                else
                {
                    // Different list type - flush current and start new
                    FlushList(result, currentListItems, currentListType);
                    currentListItems.Clear();
                    currentListItems.Add((elem, listMatch));
                    currentListType = listMatch.Type;
                }
            }
            else
            {
                FlushList(result, currentListItems, currentListType);
                currentListItems.Clear();
                currentListType = null;
                result.Add(elem);
            }
        }

        FlushList(result, currentListItems, currentListType);

        return result;
    }

    private static void FlushList(
        List<ContentElement> result,
        List<(ContentElement Element, ListLabel Label)> items,
        ListType? listType)
    {
        if (items.Count < 2)
        {
            // Not enough items for a list, add as-is
            foreach (var item in items)
                result.Add(item.Element);
            return;
        }

        // Build a PdfList
        var listItems = new List<PdfListItem>();
        BoundingBox? listBbox = null;

        foreach (var (elem, label) in items)
        {
            var text = elem.TextValue ?? string.Empty;

            // Remove the label from the text
            var bodyText = RemoveLabel(text, label);

            var bodyParagraph = new SemanticParagraph
            {
                Base = new SemanticTextNode
                {
                    BBox = elem.BBox,
                    SemanticType = SemanticType.Paragraph,
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
                                                new()
                                                {
                                                    Value = bodyText,
                                                    BBox = elem.BBox
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

            listItems.Add(new PdfListItem
            {
                Label = label,
                Body = bodyParagraph
            });

            listBbox = listBbox == null ? elem.BBox : listBbox.Union(elem.BBox);
        }

        var pdfList = new PdfList
        {
            BBox = listBbox ?? new BoundingBox(),
            Items = listItems,
            ListTypeValue = listType ?? ListType.Unordered
        };

        result.Add(ContentElement.FromList(pdfList));
    }

    private static ListLabel? TryMatchListItem(string text)
    {
        text = text.TrimStart();

        // Check bullet characters
        if (text.Length > 1 && BulletChars.Contains(text[0]) && char.IsWhiteSpace(text[1]))
        {
            return new ListLabel
            {
                Value = text[0].ToString(),
                Type = ListType.Unordered
            };
        }

        // Check bracket number [1], [2], etc.
        var bracketMatch = BracketNumberRegex.Match(text);
        if (bracketMatch.Success)
        {
            return new ListLabel
            {
                Value = bracketMatch.Groups[0].Value.Trim(),
                Type = ListType.BracketNumber
            };
        }

        // Check ordered list 1. 2. 1) 2)
        var orderedMatch = OrderedListRegex.Match(text);
        if (orderedMatch.Success)
        {
            return new ListLabel
            {
                Value = orderedMatch.Groups[0].Value.Trim(),
                Type = ListType.Ordered
            };
        }

        // Check letter list a. b. a) b)
        var letterMatch = LetterListRegex.Match(text);
        if (letterMatch.Success)
        {
            return new ListLabel
            {
                Value = letterMatch.Groups[0].Value.Trim(),
                Type = ListType.Ordered
            };
        }

        return null;
    }

    private static string RemoveLabel(string text, ListLabel label)
    {
        text = text.TrimStart();

        if (label.Type == ListType.Unordered && text.Length > 1 && BulletChars.Contains(text[0]))
        {
            return text[1..].TrimStart();
        }

        var bracketMatch = BracketNumberRegex.Match(text);
        if (bracketMatch.Success)
            return text[bracketMatch.Length..];

        var orderedMatch = OrderedListRegex.Match(text);
        if (orderedMatch.Success)
            return text[orderedMatch.Length..];

        var letterMatch = LetterListRegex.Match(text);
        if (letterMatch.Success)
            return text[letterMatch.Length..];

        return text;
    }
}
