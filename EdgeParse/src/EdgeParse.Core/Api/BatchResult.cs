namespace EdgeParse.Core.Api;

using EdgeParse.Core.Models;

/// <summary>
/// Result of batch processing multiple PDF files.
/// </summary>
public class BatchResult
{
    public List<DocumentResult> Results { get; set; } = new();
    public int TotalFiles { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public double TotalTimeMs { get; set; }
}

public class DocumentResult
{
    public string FilePath { get; set; } = string.Empty;
    public PdfDocument? Document { get; set; }
    public string? Error { get; set; }
    public bool Success => Document != null;
    public double ProcessingTimeMs { get; set; }
}
