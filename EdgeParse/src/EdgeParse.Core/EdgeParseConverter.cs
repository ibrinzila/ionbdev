namespace EdgeParse.Core;

using EdgeParse.Core.Api;
using EdgeParse.Core.Models;
using EdgeParse.Core.Output;
using EdgeParse.Core.Pdf;
using EdgeParse.Core.Pipeline;

/// <summary>
/// Main entry point for EdgeParse PDF conversion.
/// Mirrors the Rust lib.rs convert/convert_bytes functions.
/// </summary>
public static class EdgeParseConverter
{
    /// <summary>
    /// Converts a PDF file to a PdfDocument.
    /// </summary>
    public static PdfDocument Convert(string inputPath, ProcessingConfig? config = null)
    {
        config ??= new ProcessingConfig();
        var fileName = Path.GetFileName(inputPath);

        var pageChunks = PdfTextExtractor.ExtractFromFile(inputPath, config.Password);
        var metadata = PdfTextExtractor.GetMetadata(inputPath, config.Password);

        var orchestrator = new Orchestrator(config);
        return orchestrator.Process(pageChunks, metadata, fileName);
    }

    /// <summary>
    /// Converts PDF bytes to a PdfDocument.
    /// </summary>
    public static PdfDocument ConvertBytes(byte[] data, string fileName, ProcessingConfig? config = null)
    {
        config ??= new ProcessingConfig();

        var pageChunks = PdfTextExtractor.ExtractFromBytes(data, config.Password);
        var metadata = PdfTextExtractor.GetMetadataFromBytes(data, config.Password);

        var orchestrator = new Orchestrator(config);
        return orchestrator.Process(pageChunks, metadata, fileName);
    }

    /// <summary>
    /// Converts a PDF file to a specific output format string.
    /// </summary>
    public static string ConvertToString(string inputPath, OutputFormat format, ProcessingConfig? config = null)
    {
        var doc = Convert(inputPath, config);
        return RenderDocument(doc, format, config);
    }

    /// <summary>
    /// Converts PDF bytes to a specific output format string.
    /// </summary>
    public static string ConvertBytesToString(byte[] data, string fileName, OutputFormat format, ProcessingConfig? config = null)
    {
        var doc = ConvertBytes(data, fileName, config);
        return RenderDocument(doc, format, config);
    }

    /// <summary>
    /// Batch processes multiple PDF files.
    /// </summary>
    public static BatchResult ProcessBatch(IEnumerable<string> filePaths, ProcessingConfig? config = null)
    {
        config ??= new ProcessingConfig();
        var result = new BatchResult();
        var paths = filePaths.ToList();
        result.TotalFiles = paths.Count;

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Process in parallel
        var results = new DocumentResult[paths.Count];
        Parallel.For(0, paths.Count, i =>
        {
            var itemSw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var doc = Convert(paths[i], config);
                results[i] = new DocumentResult
                {
                    FilePath = paths[i],
                    Document = doc,
                    ProcessingTimeMs = itemSw.Elapsed.TotalMilliseconds
                };
            }
            catch (Exception ex)
            {
                results[i] = new DocumentResult
                {
                    FilePath = paths[i],
                    Error = ex.Message,
                    ProcessingTimeMs = itemSw.Elapsed.TotalMilliseconds
                };
            }
        });

        sw.Stop();
        result.Results = results.ToList();
        result.SuccessCount = results.Count(r => r.Success);
        result.FailureCount = results.Count(r => !r.Success);
        result.TotalTimeMs = sw.Elapsed.TotalMilliseconds;

        return result;
    }

    /// <summary>
    /// Renders a PdfDocument to a string in the specified format.
    /// </summary>
    public static string RenderDocument(PdfDocument doc, OutputFormat format, ProcessingConfig? config = null)
    {
        return format switch
        {
            OutputFormat.Markdown => MarkdownRenderer.Render(doc, config?.MarkdownPageSeparator),
            OutputFormat.Json => JsonRenderer.Render(doc),
            OutputFormat.Html => HtmlRenderer.Render(doc, config?.HtmlPageSeparator),
            OutputFormat.Text => TextRenderer.Render(doc, config?.TextPageSeparator),
            _ => throw new ArgumentException($"Unsupported format: {format}")
        };
    }
}
