namespace EdgeParse.Core.Output;

using EdgeParse.Core.Models;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Renders a PdfDocument as GitHub Flavored Markdown.
/// Handles heading levels, tables, lists, figures, and page separators.
/// </summary>
public static class MarkdownRenderer
{
    // Caption prefixes that should demote headings to paragraphs in markdown
    private static readonly Regex CaptionPrefixRegex = new(
        @"^(Figure|Fig\.|Table|Tab\.|Source:|Image|Plate)\s",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string Render(PdfDocument doc, string? pageSeparator = null)
    {
        var sb = new StringBuilder();
        int? lastPage = null;

        foreach (var element in doc.Kids)
        {
            // Page separator
            if (pageSeparator != null && lastPage != null && element.PageNumber != lastPage)
            {
                sb.AppendLine(pageSeparator);
                sb.AppendLine();
            }
            lastPage = element.PageNumber;

            RenderElement(sb, element);
        }

        return sb.ToString().TrimEnd();
    }

    private static void RenderElement(StringBuilder sb, ContentElement element)
    {
        switch (element.Type)
        {
            case ContentElementType.Heading:
                RenderHeading(sb, element.Heading!);
                break;
            case ContentElementType.NumberHeading:
                RenderHeading(sb, element.NumberHeading!.Base);
                break;
            case ContentElementType.Paragraph:
                RenderParagraph(sb, element.Paragraph!);
                break;
            case ContentElementType.Table:
                RenderTable(sb, element.Table!);
                break;
            case ContentElementType.List:
                RenderList(sb, element.List!);
                break;
            case ContentElementType.Caption:
                RenderCaption(sb, element.Caption!);
                break;
            case ContentElementType.Figure:
                RenderFigure(sb, element.Figure!);
                break;
            case ContentElementType.Formula:
                RenderFormula(sb, element.Formula!);
                break;
            case ContentElementType.HeaderFooter:
                RenderHeaderFooter(sb, element.HeaderFooter!);
                break;
            case ContentElementType.TextBlock:
                sb.AppendLine(element.TextBlock!.Value());
                sb.AppendLine();
                break;
            case ContentElementType.TextLine:
                sb.AppendLine(element.TextLine!.Value());
                sb.AppendLine();
                break;
            case ContentElementType.TextChunk:
                sb.Append(element.TextChunk!.Value);
                break;
        }
    }

    private static void RenderHeading(StringBuilder sb, SemanticHeading heading)
    {
        var text = heading.Value().Trim();
        if (string.IsNullOrEmpty(text)) return;

        // Demote false-positive headings
        if (ShouldDemoteHeading(text))
        {
            sb.AppendLine(text);
            sb.AppendLine();
            return;
        }

        int level = heading.HeadingLevel ?? 1;
        level = Math.Clamp(level, 1, 6);

        sb.Append(new string('#', level));
        sb.Append(' ');
        sb.AppendLine(text);
        sb.AppendLine();
    }

    private static bool ShouldDemoteHeading(string text)
    {
        // Caption prefix
        if (CaptionPrefixRegex.IsMatch(text)) return true;

        // Ends with period and contains spaces (likely a sentence)
        if (text.EndsWith('.') && text.Contains(' ') && text.Length > 60) return true;

        // Contains math symbols (likely formula)
        if (text.Any(c => "∑∫∏∂∇≈≠≤≥±√∞".Contains(c))) return true;

        return false;
    }

    private static void RenderParagraph(StringBuilder sb, SemanticParagraph para)
    {
        var text = para.Value().Trim();
        if (string.IsNullOrEmpty(text)) return;

        // Apply inline formatting
        text = ApplyInlineFormatting(text, para.Base);

        sb.AppendLine(text);
        sb.AppendLine();
    }

    private static string ApplyInlineFormatting(string text, SemanticTextNode node)
    {
        // Bold text
        if (node.FontWeight >= 700)
        {
            text = $"**{text}**";
        }
        // Italic text
        else if (node.ItalicAngle.HasValue && node.ItalicAngle.Value != 0)
        {
            text = $"*{text}*";
        }

        return text;
    }

    private static void RenderTable(StringBuilder sb, SemanticTable table)
    {
        if (table.Caption != null)
        {
            sb.AppendLine(table.Caption.Value().Trim());
            sb.AppendLine();
        }

        var border = table.Table;
        if (border.Rows.Count == 0) return;

        // Determine max columns
        int maxCols = border.NumColumns;
        if (maxCols == 0) maxCols = border.Rows.Max(r => r.Cells.Count);

        // Render header row (first row)
        var headerRow = border.Rows[0];
        sb.Append('|');
        for (int c = 0; c < maxCols; c++)
        {
            string cellText = c < headerRow.Cells.Count
                ? EscapeTableCell(headerRow.Cells[c].TextContent)
                : "";
            sb.Append($" {cellText} |");
        }
        sb.AppendLine();

        // Separator row
        sb.Append('|');
        for (int c = 0; c < maxCols; c++)
        {
            sb.Append(" --- |");
        }
        sb.AppendLine();

        // Data rows
        for (int r = 1; r < border.Rows.Count; r++)
        {
            var row = border.Rows[r];
            sb.Append('|');
            for (int c = 0; c < maxCols; c++)
            {
                string cellText = c < row.Cells.Count
                    ? EscapeTableCell(row.Cells[c].TextContent)
                    : "";
                sb.Append($" {cellText} |");
            }
            sb.AppendLine();
        }

        sb.AppendLine();
    }

    private static string EscapeTableCell(string text)
    {
        return text.Replace("|", "\\|").Replace("\n", " ").Trim();
    }

    private static void RenderList(StringBuilder sb, PdfList list)
    {
        for (int i = 0; i < list.Items.Count; i++)
        {
            var item = list.Items[i];
            string prefix;

            switch (list.ListTypeValue)
            {
                case ListType.Ordered:
                    prefix = $"{i + 1}. ";
                    break;
                case ListType.BracketNumber:
                    prefix = $"[{i + 1}] ";
                    break;
                default:
                    prefix = "- ";
                    break;
            }

            var bodyText = item.Body.Value().Trim();
            sb.AppendLine($"{prefix}{bodyText}");
        }
        sb.AppendLine();
    }

    private static void RenderCaption(StringBuilder sb, SemanticCaption caption)
    {
        var text = caption.Value().Trim();
        if (!string.IsNullOrEmpty(text))
        {
            sb.AppendLine($"*{text}*");
            sb.AppendLine();
        }
    }

    private static void RenderFigure(StringBuilder sb, SemanticFigure figure)
    {
        sb.AppendLine("[Figure]");
        if (figure.Caption != null)
        {
            sb.AppendLine($"*{figure.Caption.Value().Trim()}*");
        }
        sb.AppendLine();
    }

    private static void RenderFormula(StringBuilder sb, SemanticFormula formula)
    {
        var text = formula.Value().Trim();
        if (!string.IsNullOrEmpty(text))
        {
            sb.AppendLine($"$${text}$$");
            sb.AppendLine();
        }
    }

    private static void RenderHeaderFooter(StringBuilder sb, SemanticHeaderOrFooter hf)
    {
        var text = hf.Value().Trim();
        if (!string.IsNullOrEmpty(text))
        {
            sb.AppendLine(text);
            sb.AppendLine();
        }
    }
}
