using Microsoft.AspNetCore.Mvc;
using EdgeParse.Core;
using EdgeParse.Core.Api;
using EdgeParse.Core.Models;

namespace EdgeParse.Web.Controllers;

/// <summary>
/// PDF parsing endpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ParseController : ControllerBase
{
    private readonly ILogger<ParseController> _logger;

    public ParseController(ILogger<ParseController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parse a PDF file and return structured output.
    /// Upload a PDF file with optional configuration parameters.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(100_000_000)] // 100 MB
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Parse(
        IFormFile file,
        [FromQuery] string format = "markdown",
        [FromQuery] string? pages = null,
        [FromQuery] string readingOrder = "xycut",
        [FromQuery] string tableMethod = "default",
        [FromQuery] bool includeHeaderFooter = false,
        [FromQuery] bool filterHiddenText = true,
        [FromQuery] bool filterOutOfPage = true,
        [FromQuery] bool filterTinyText = true,
        [FromQuery] string? password = null)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file uploaded" });

        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Only PDF files are supported" });

        try
        {
            var config = BuildConfig(format, pages, readingOrder, tableMethod,
                includeHeaderFooter, filterHiddenText, filterOutOfPage, filterTinyText, password);

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var data = ms.ToArray();

            var outputFormat = ParseOutputFormat(format);

            _logger.LogInformation("Parsing PDF: {FileName} ({Size} bytes) -> {Format}",
                file.FileName, data.Length, format);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = EdgeParseConverter.ConvertBytesToString(data, file.FileName, outputFormat, config);
            sw.Stop();

            _logger.LogInformation("Parsed {FileName} in {ElapsedMs:F1}ms",
                file.FileName, sw.Elapsed.TotalMilliseconds);

            var contentType = outputFormat switch
            {
                OutputFormat.Json => "application/json",
                OutputFormat.Html => "text/html",
                OutputFormat.Markdown => "text/markdown",
                _ => "text/plain"
            };

            return Content(result, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing PDF: {FileName}", file.FileName);
            return StatusCode(500, new { error = "Failed to parse PDF", message = ex.Message });
        }
    }

    /// <summary>
    /// Parse a PDF and return structured JSON with bounding boxes and metadata.
    /// </summary>
    [HttpPost("document")]
    [RequestSizeLimit(100_000_000)]
    [ProducesResponseType(typeof(PdfDocument), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> ParseDocument(
        IFormFile file,
        [FromQuery] string? pages = null,
        [FromQuery] string readingOrder = "xycut",
        [FromQuery] string tableMethod = "default",
        [FromQuery] bool includeHeaderFooter = false,
        [FromQuery] string? password = null)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file uploaded" });

        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Only PDF files are supported" });

        try
        {
            var config = BuildConfig("json", pages, readingOrder, tableMethod,
                includeHeaderFooter, true, true, true, password);

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var data = ms.ToArray();

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var doc = EdgeParseConverter.ConvertBytes(data, file.FileName, config);
            sw.Stop();

            var jsonOutput = EdgeParseConverter.RenderDocument(doc, OutputFormat.Json, config);

            return Content(jsonOutput, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing PDF: {FileName}", file.FileName);
            return StatusCode(500, new { error = "Failed to parse PDF", message = ex.Message });
        }
    }

    /// <summary>
    /// Parse multiple PDFs in a batch.
    /// </summary>
    [HttpPost("batch")]
    [RequestSizeLimit(500_000_000)] // 500 MB
    [ProducesResponseType(typeof(BatchResponse), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> ParseBatch(
        List<IFormFile> files,
        [FromQuery] string format = "markdown",
        [FromQuery] string? pages = null,
        [FromQuery] string readingOrder = "xycut",
        [FromQuery] string tableMethod = "default",
        [FromQuery] string? password = null)
    {
        if (files == null || files.Count == 0)
            return BadRequest(new { error = "No files uploaded" });

        var config = BuildConfig(format, pages, readingOrder, tableMethod,
            false, true, true, true, password);
        var outputFormat = ParseOutputFormat(format);

        var results = new List<BatchFileResult>();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        foreach (var file in files)
        {
            if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new BatchFileResult
                {
                    FileName = file.FileName,
                    Error = "Not a PDF file"
                });
                continue;
            }

            try
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                var data = ms.ToArray();

                var itemSw = System.Diagnostics.Stopwatch.StartNew();
                var output = EdgeParseConverter.ConvertBytesToString(data, file.FileName, outputFormat, config);
                itemSw.Stop();

                results.Add(new BatchFileResult
                {
                    FileName = file.FileName,
                    Content = output,
                    ProcessingTimeMs = itemSw.Elapsed.TotalMilliseconds
                });
            }
            catch (Exception ex)
            {
                results.Add(new BatchFileResult
                {
                    FileName = file.FileName,
                    Error = ex.Message
                });
            }
        }

        sw.Stop();

        return Ok(new BatchResponse
        {
            TotalFiles = files.Count,
            SuccessCount = results.Count(r => r.Error == null),
            FailureCount = results.Count(r => r.Error != null),
            TotalTimeMs = sw.Elapsed.TotalMilliseconds,
            Results = results
        });
    }

    private static ProcessingConfig BuildConfig(
        string format, string? pages, string readingOrder, string tableMethod,
        bool includeHeaderFooter, bool filterHiddenText, bool filterOutOfPage,
        bool filterTinyText, string? password)
    {
        return new ProcessingConfig
        {
            Formats = new List<OutputFormat> { ParseOutputFormat(format) },
            Pages = pages,
            ReadingOrder = readingOrder.ToLowerInvariant() == "off"
                ? ReadingOrder.Off
                : ReadingOrder.XyCut,
            TableMethod = tableMethod.ToLowerInvariant() == "cluster"
                ? TableMethod.Cluster
                : TableMethod.Default,
            IncludeHeaderFooter = includeHeaderFooter,
            Password = password,
            FilterConfig = new FilterConfig
            {
                FilterHiddenText = filterHiddenText,
                FilterOutOfPage = filterOutOfPage,
                FilterTinyText = filterTinyText
            }
        };
    }

    private static OutputFormat ParseOutputFormat(string format)
    {
        return format.ToLowerInvariant() switch
        {
            "json" => OutputFormat.Json,
            "html" => OutputFormat.Html,
            "text" or "txt" => OutputFormat.Text,
            _ => OutputFormat.Markdown
        };
    }
}

public class BatchResponse
{
    public int TotalFiles { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public double TotalTimeMs { get; set; }
    public List<BatchFileResult> Results { get; set; } = new();
}

public class BatchFileResult
{
    public string FileName { get; set; } = string.Empty;
    public string? Content { get; set; }
    public string? Error { get; set; }
    public double ProcessingTimeMs { get; set; }
}
