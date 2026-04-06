namespace EdgeParse.Core.Output;

using EdgeParse.Core.Models;
using System.Text;

/// <summary>
/// Renders a PdfDocument as plain UTF-8 text with reading order preserved.
/// </summary>
public static class TextRenderer
{
    public static string Render(PdfDocument doc, string? pageSeparator = null)
    {
        var sb = new StringBuilder();
        int? lastPage = null;

        foreach (var element in doc.Kids)
        {
            if (pageSeparator != null && lastPage != null && element.PageNumber != lastPage)
            {
                sb.AppendLine(pageSeparator);
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
                sb.AppendLine(element.Heading!.Value().Trim());
                sb.AppendLine();
                break;

            case ContentElementType.NumberHeading:
                sb.AppendLine(element.NumberHeading!.Value().Trim());
                sb.AppendLine();
                break;

            case ContentElementType.Paragraph:
                sb.AppendLine(element.Paragraph!.Value().Trim());
                sb.AppendLine();
                break;

            case ContentElementType.Table:
                RenderTable(sb, element.Table!);
                break;

            case ContentElementType.List:
                RenderList(sb, element.List!);
                break;

            case ContentElementType.Caption:
                sb.AppendLine(element.Caption!.Value().Trim());
                sb.AppendLine();
                break;

            case ContentElementType.Figure:
                sb.AppendLine("[Figure]");
                if (element.Figure!.Caption != null)
                    sb.AppendLine(element.Figure.Caption.Value().Trim());
                sb.AppendLine();
                break;

            case ContentElementType.Formula:
                sb.AppendLine(element.Formula!.Value().Trim());
                sb.AppendLine();
                break;

            case ContentElementType.HeaderFooter:
                sb.AppendLine(element.HeaderFooter!.Value().Trim());
                sb.AppendLine();
                break;

            case ContentElementType.TextBlock:
                sb.AppendLine(element.TextBlock!.Value().Trim());
                sb.AppendLine();
                break;

            case ContentElementType.TextLine:
                sb.AppendLine(element.TextLine!.Value().Trim());
                sb.AppendLine();
                break;
        }
    }

    private static void RenderTable(StringBuilder sb, SemanticTable table)
    {
        if (table.Caption != null)
        {
            sb.AppendLine(table.Caption.Value().Trim());
        }

        foreach (var row in table.Table.Rows)
        {
            var cellTexts = row.Cells.Select(c => c.TextContent.Trim());
            sb.AppendLine(string.Join("\t", cellTexts));
        }
        sb.AppendLine();
    }

    private static void RenderList(StringBuilder sb, PdfList list)
    {
        for (int i = 0; i < list.Items.Count; i++)
        {
            var item = list.Items[i];
            string prefix = list.ListTypeValue switch
            {
                ListType.Ordered => $"{i + 1}. ",
                ListType.BracketNumber => $"[{i + 1}] ",
                _ => "- "
            };
            sb.AppendLine($"{prefix}{item.Body.Value().Trim()}");
        }
        sb.AppendLine();
    }
}
