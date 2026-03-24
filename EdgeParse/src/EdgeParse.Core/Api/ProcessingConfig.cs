namespace EdgeParse.Core.Api;

using EdgeParse.Core.Models;

/// <summary>
/// Configuration for PDF processing pipeline.
/// Mirrors the Rust ProcessingConfig with all 24 options.
/// </summary>
public class ProcessingConfig
{
    // I/O
    public string? OutputDir { get; set; }
    public string? Password { get; set; }

    // Output formats
    public List<OutputFormat> Formats { get; set; } = new() { OutputFormat.Markdown };

    // Content filtering
    public FilterConfig FilterConfig { get; set; } = new();
    public bool Sanitize { get; set; }

    // Extraction options
    public ReadingOrder ReadingOrder { get; set; } = ReadingOrder.XyCut;
    public TableMethod TableMethod { get; set; } = TableMethod.Default;
    public bool UseStructTree { get; set; }
    public string? Pages { get; set; }

    // Image handling
    public ImageOutput ImageOutput { get; set; } = ImageOutput.Off;
    public ImageFormat ImageFormat { get; set; } = ImageFormat.Png;
    public string? ImageDir { get; set; }

    // Separators
    public string? MarkdownPageSeparator { get; set; }
    public string? TextPageSeparator { get; set; }
    public string? HtmlPageSeparator { get; set; }

    // Header/footer inclusion
    public bool IncludeHeaderFooter { get; set; }

    /// <summary>
    /// Parses the Pages string (e.g., "1,3,5-7") into a set of page numbers.
    /// </summary>
    public HashSet<int>? ParsePageSet()
    {
        if (string.IsNullOrWhiteSpace(Pages)) return null;

        var result = new HashSet<int>();
        var parts = Pages.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            if (part.Contains('-'))
            {
                var range = part.Split('-', 2);
                if (int.TryParse(range[0].Trim(), out int start) && int.TryParse(range[1].Trim(), out int end))
                {
                    for (int i = start; i <= end; i++)
                        result.Add(i);
                }
            }
            else if (int.TryParse(part, out int page))
            {
                result.Add(page);
            }
        }

        return result.Count > 0 ? result : null;
    }
}

/// <summary>
/// Configuration for content safety filtering.
/// </summary>
public class FilterConfig
{
    /// <summary>Remove low-contrast (hidden) text. Default true.</summary>
    public bool FilterHiddenText { get; set; } = true;

    /// <summary>Remove text outside the CropBox. Default true.</summary>
    public bool FilterOutOfPage { get; set; } = true;

    /// <summary>Remove very small text (height below minimum). Default true.</summary>
    public bool FilterTinyText { get; set; } = true;

    /// <summary>Remove invisible Optional Content Group layers. Default true.</summary>
    public bool FilterHiddenOcg { get; set; } = true;

    /// <summary>Minimum contrast ratio threshold for hidden text detection.</summary>
    public double ContrastRatioThreshold { get; set; } = 1.5;

    /// <summary>Minimum font size in points for tiny text detection.</summary>
    public double MinFontSize { get; set; } = 3.0;
}
