namespace EdgeParse.Core;

using EdgeParse.Core.Api;
using EdgeParse.Core.Models;
using EdgeParse.Core.Output;
using EdgeParse.Core.Pdf;
using EdgeParse.Core.Pipeline;

/// <summary>
/// Main entry point for EdgeParse PDF conversion.
/// Optimized: single PDF open for metadata+content, parallel batch processing.
/// </summary>
public static class EdgeParseConverter
{
    /// <summary>
    /// Converts a PDF file to a PdfDocument.
    /// Uses single-open extraction (metadata + pages in one pass).
    /// </summary>
    public static PdfDocument Convert(string inputPath, ProcessingConfig? config = null)
    {
        config ??= new ProcessingConfig();
        var fileName = Path.GetFileName(inputPath);

        // Single open - extract metadata and pages together
        var (metadata, pageChunks) = PdfTextExtractor.ExtractAll(inputPath, config.Password);

        var orchestrator = new Orchestrator(config);
        return orchestrator.Process(pageChunks, metadata, fileName);
    }

    /// <summary>
    /// Converts PDF bytes to a PdfDocument.
    /// Uses single-open extraction.
    /// </summary>
    public static PdfDocument ConvertBytes(byte[] data, string fileName, ProcessingConfig? config = null)
    {
        config ??= new ProcessingConfig();

        var (metadata, pageChunks) = PdfTextExtractor.ExtractAllFromBytes(data, config.Password);

        var orchestrator = new Orchestrator(config);
        return orchestrator.Process(pageChunks, metadata, fileName);
    }

    public static string ConvertToString(string inputPath, OutputFormat format, ProcessingConfig? config = null)
    {
        var doc = Convert(inputPath, config);
        return RenderDocument(doc, format, config);
    }

    public static string ConvertBytesToString(byte[] data, string fileName, OutputFormat format, ProcessingConfig? config = null)
    {
        var doc = ConvertBytes(data, fileName, config);
        return RenderDocument(doc, format, config);
    }

    /// <summary>
    /// Batch processes multiple PDF files with full parallelism.
    /// </summary>
    public static BatchResult ProcessBatch(IEnumerable<string> filePaths, ProcessingConfig? config = null)
    {
        config ??= new ProcessingConfig();
        var paths = filePaths.ToList();
        var results = new DocumentResult[paths.Count];
        var sw = System.Diagnostics.Stopwatch.StartNew();

        Parallel.For(0, paths.Count, new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount)
        }, i =>
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
        return new BatchResult
        {
            TotalFiles = paths.Count,
            Results = results.ToList(),
            SuccessCount = results.Count(r => r.Success),
            FailureCount = results.Count(r => !r.Success),
            TotalTimeMs = sw.Elapsed.TotalMilliseconds
        };
    }

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
