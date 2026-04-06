namespace EdgeParse.Core.Models;

/// <summary>
/// The final output document containing all extracted content in reading order.
/// </summary>
public class PdfDocument
{
    public string FileName { get; set; } = string.Empty;
    public int NumberOfPages { get; set; }
    public string? Author { get; set; }
    public string? Title { get; set; }
    public string? CreationDate { get; set; }
    public string? ModificationDate { get; set; }
    public string? Producer { get; set; }
    public string? Creator { get; set; }
    public string? Subject { get; set; }
    public string? Keywords { get; set; }
    public List<ContentElement> Kids { get; set; } = new();
}
