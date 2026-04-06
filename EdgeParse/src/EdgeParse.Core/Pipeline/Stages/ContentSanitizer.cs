namespace EdgeParse.Core.Pipeline.Stages;

using EdgeParse.Core.Models;
using System.Text.RegularExpressions;

/// <summary>
/// Stage 19: Content sanitization - redacts PII (emails, phone numbers, SSNs, etc.)
/// and normalizes text encoding.
/// </summary>
public static class ContentSanitizer
{
    // PII patterns
    private static readonly Regex EmailRegex = new(
        @"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}",
        RegexOptions.Compiled);

    private static readonly Regex PhoneRegex = new(
        @"(\+?\d{1,3}[\s\-.]?)?\(?\d{3}\)?[\s\-.]?\d{3}[\s\-.]?\d{4}",
        RegexOptions.Compiled);

    private static readonly Regex SsnRegex = new(
        @"\b\d{3}[\-\s]?\d{2}[\-\s]?\d{4}\b",
        RegexOptions.Compiled);

    private static readonly Regex CreditCardRegex = new(
        @"\b\d{4}[\s\-]?\d{4}[\s\-]?\d{4}[\s\-]?\d{4}\b",
        RegexOptions.Compiled);

    private const string Redacted = "[REDACTED]";

    public static List<ContentElement> Sanitize(List<ContentElement> elements)
    {
        foreach (var elem in elements)
        {
            SanitizeElement(elem);
        }
        return elements;
    }

    private static void SanitizeElement(ContentElement elem)
    {
        switch (elem.Type)
        {
            case ContentElementType.TextChunk:
                elem.TextChunk!.Value = RedactPii(elem.TextChunk.Value);
                break;

            case ContentElementType.TextLine:
                foreach (var chunk in elem.TextLine!.Chunks)
                    chunk.Value = RedactPii(chunk.Value);
                break;

            case ContentElementType.TextBlock:
                foreach (var line in elem.TextBlock!.Lines)
                    foreach (var chunk in line.Chunks)
                        chunk.Value = RedactPii(chunk.Value);
                break;

            case ContentElementType.Paragraph:
                SanitizeSemanticNode(elem.Paragraph!.Base);
                break;

            case ContentElementType.Heading:
                SanitizeSemanticNode(elem.Heading!.Base.Base);
                break;

            case ContentElementType.Caption:
                SanitizeSemanticNode(elem.Caption!.Base.Base);
                break;

            case ContentElementType.HeaderFooter:
                SanitizeSemanticNode(elem.HeaderFooter!.Base.Base);
                break;

            case ContentElementType.Table:
                foreach (var row in elem.Table!.Table.Rows)
                    foreach (var cell in row.Cells)
                        for (int i = 0; i < cell.Content.Count; i++)
                            cell.Content[i] = RedactPii(cell.Content[i]);
                break;

            case ContentElementType.List:
                foreach (var item in elem.List!.Items)
                    SanitizeSemanticNode(item.Body.Base);
                break;
        }
    }

    private static void SanitizeSemanticNode(SemanticTextNode node)
    {
        foreach (var column in node.Columns)
            foreach (var block in column.TextBlocks)
                foreach (var line in block.Lines)
                    foreach (var chunk in line.Chunks)
                        chunk.Value = RedactPii(chunk.Value);
    }

    private static string RedactPii(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        // Normalize encoding: replace common Unicode issues
        text = NormalizeText(text);

        // Redact PII
        text = EmailRegex.Replace(text, Redacted);
        text = CreditCardRegex.Replace(text, Redacted);
        text = SsnRegex.Replace(text, Redacted);
        text = PhoneRegex.Replace(text, Redacted);

        return text;
    }

    private static string NormalizeText(string text)
    {
        // Replace common Unicode normalization issues
        text = text.Replace('\u00A0', ' ');  // Non-breaking space → space
        text = text.Replace('\u2028', '\n'); // Line separator → newline
        text = text.Replace('\u2029', '\n'); // Paragraph separator → newline
        text = text.Replace("\r\n", "\n");   // CRLF → LF
        text = text.Replace('\r', '\n');     // CR → LF

        // Replace ligatures with individual characters
        text = text.Replace("\uFB01", "fi");
        text = text.Replace("\uFB02", "fl");
        text = text.Replace("\uFB03", "ffi");
        text = text.Replace("\uFB04", "ffl");

        return text;
    }
}
