namespace EdgeParse.Core.Output;

using EdgeParse.Core.Models;
using System.Text;
using System.Web;

/// <summary>
/// Renders a PdfDocument as semantic HTML5 with CSS classes.
/// </summary>
public static class HtmlRenderer
{
    public static string Render(PdfDocument doc, string? pageSeparator = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine($"  <title>{HttpUtility.HtmlEncode(doc.Title ?? doc.FileName)}</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    body { font-family: system-ui, -apple-system, sans-serif; max-width: 900px; margin: 0 auto; padding: 2rem; line-height: 1.6; }");
        sb.AppendLine("    table { border-collapse: collapse; width: 100%; margin: 1rem 0; }");
        sb.AppendLine("    th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
        sb.AppendLine("    th { background-color: #f5f5f5; font-weight: bold; }");
        sb.AppendLine("    .caption { font-style: italic; color: #666; margin: 0.5rem 0; }");
        sb.AppendLine("    .figure { text-align: center; margin: 1rem 0; }");
        sb.AppendLine("    .header-footer { color: #999; font-size: 0.85em; }");
        sb.AppendLine("    .formula { font-family: 'Computer Modern', serif; text-align: center; margin: 1rem 0; }");
        sb.AppendLine("    .page-break { border-top: 1px dashed #ccc; margin: 2rem 0; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        int? lastPage = null;
        foreach (var element in doc.Kids)
        {
            if (pageSeparator != null && lastPage != null && element.PageNumber != lastPage)
            {
                sb.AppendLine("  <hr class=\"page-break\">");
            }
            lastPage = element.PageNumber;

            RenderElement(sb, element);
        }

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
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
                sb.AppendLine($"  <p class=\"caption\">{Encode(element.Caption!.Value())}</p>");
                break;
            case ContentElementType.Figure:
                RenderFigure(sb, element.Figure!);
                break;
            case ContentElementType.Formula:
                sb.AppendLine($"  <div class=\"formula\">{Encode(element.Formula!.Value())}</div>");
                break;
            case ContentElementType.HeaderFooter:
                sb.AppendLine($"  <div class=\"header-footer\">{Encode(element.HeaderFooter!.Value())}</div>");
                break;
            case ContentElementType.TextBlock:
                sb.AppendLine($"  <p>{Encode(element.TextBlock!.Value())}</p>");
                break;
            case ContentElementType.TextLine:
                sb.AppendLine($"  <p>{Encode(element.TextLine!.Value())}</p>");
                break;
        }
    }

    private static void RenderHeading(StringBuilder sb, SemanticHeading heading)
    {
        int level = Math.Clamp(heading.HeadingLevel ?? 1, 1, 6);
        sb.AppendLine($"  <h{level}>{Encode(heading.Value())}</h{level}>");
    }

    private static void RenderParagraph(StringBuilder sb, SemanticParagraph para)
    {
        var text = para.Value().Trim();
        if (string.IsNullOrEmpty(text)) return;

        var node = para.Base;
        if (node.FontWeight >= 700)
            sb.AppendLine($"  <p><strong>{Encode(text)}</strong></p>");
        else if (node.ItalicAngle.HasValue && node.ItalicAngle.Value != 0)
            sb.AppendLine($"  <p><em>{Encode(text)}</em></p>");
        else
            sb.AppendLine($"  <p>{Encode(text)}</p>");
    }

    private static void RenderTable(StringBuilder sb, SemanticTable table)
    {
        if (table.Caption != null)
        {
            sb.AppendLine($"  <p class=\"caption\">{Encode(table.Caption.Value())}</p>");
        }

        var border = table.Table;
        if (border.Rows.Count == 0) return;

        sb.AppendLine("  <table>");

        // Header row
        sb.AppendLine("    <thead>");
        sb.AppendLine("      <tr>");
        if (border.Rows.Count > 0)
        {
            foreach (var cell in border.Rows[0].Cells)
            {
                var attrs = BuildCellAttrs(cell);
                sb.AppendLine($"        <th{attrs}>{Encode(cell.TextContent)}</th>");
            }
        }
        sb.AppendLine("      </tr>");
        sb.AppendLine("    </thead>");

        // Body rows
        if (border.Rows.Count > 1)
        {
            sb.AppendLine("    <tbody>");
            for (int r = 1; r < border.Rows.Count; r++)
            {
                sb.AppendLine("      <tr>");
                foreach (var cell in border.Rows[r].Cells)
                {
                    var attrs = BuildCellAttrs(cell);
                    sb.AppendLine($"        <td{attrs}>{Encode(cell.TextContent)}</td>");
                }
                sb.AppendLine("      </tr>");
            }
            sb.AppendLine("    </tbody>");
        }

        sb.AppendLine("  </table>");
    }

    private static string BuildCellAttrs(TableBorderCell cell)
    {
        var attrs = new StringBuilder();
        if (cell.ColSpan > 1) attrs.Append($" colspan=\"{cell.ColSpan}\"");
        if (cell.RowSpan > 1) attrs.Append($" rowspan=\"{cell.RowSpan}\"");
        return attrs.ToString();
    }

    private static void RenderList(StringBuilder sb, PdfList list)
    {
        string tag = list.ListTypeValue == ListType.Unordered ? "ul" : "ol";
        sb.AppendLine($"  <{tag}>");
        foreach (var item in list.Items)
        {
            sb.AppendLine($"    <li>{Encode(item.Body.Value())}</li>");
        }
        sb.AppendLine($"  </{tag}>");
    }

    private static void RenderFigure(StringBuilder sb, SemanticFigure figure)
    {
        sb.AppendLine("  <figure class=\"figure\">");
        sb.AppendLine("    <div>[Figure]</div>");
        if (figure.Caption != null)
        {
            sb.AppendLine($"    <figcaption>{Encode(figure.Caption.Value())}</figcaption>");
        }
        sb.AppendLine("  </figure>");
    }

    private static string Encode(string text) => HttpUtility.HtmlEncode(text.Trim());
}
