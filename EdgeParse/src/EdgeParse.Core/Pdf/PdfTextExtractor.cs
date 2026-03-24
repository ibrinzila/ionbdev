namespace EdgeParse.Core.Pdf;

using EdgeParse.Core.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using PigPdfDocument = UglyToad.PdfPig.PdfDocument;

/// <summary>
/// Extracts text chunks, images, and lines from PDF pages using PdfPig.
/// Replaces the Rust chunk_parser + font resolver + text_extractor.
/// </summary>
public class PdfTextExtractor
{
    /// <summary>
    /// Extracts all content from a PDF file, returning per-page chunks.
    /// </summary>
    public static List<PageChunks> ExtractFromFile(string filePath, string? password = null)
    {
        using var document = password != null
            ? PigPdfDocument.Open(filePath, new ParsingOptions { Password = password })
            : PigPdfDocument.Open(filePath);

        return ExtractFromDocument(document);
    }

    /// <summary>
    /// Extracts all content from PDF bytes.
    /// </summary>
    public static List<PageChunks> ExtractFromBytes(byte[] data, string? password = null)
    {
        using var stream = new MemoryStream(data);
        using var document = password != null
            ? PigPdfDocument.Open(stream, new ParsingOptions { Password = password })
            : PigPdfDocument.Open(stream);

        return ExtractFromDocument(document);
    }

    /// <summary>
    /// Gets document metadata from a PDF file.
    /// </summary>
    public static PdfMetadata GetMetadata(string filePath, string? password = null)
    {
        using var document = password != null
            ? PigPdfDocument.Open(filePath, new ParsingOptions { Password = password })
            : PigPdfDocument.Open(filePath);

        return ExtractMetadata(document);
    }

    /// <summary>
    /// Gets document metadata from PDF bytes.
    /// </summary>
    public static PdfMetadata GetMetadataFromBytes(byte[] data, string? password = null)
    {
        using var stream = new MemoryStream(data);
        using var document = password != null
            ? PigPdfDocument.Open(stream, new ParsingOptions { Password = password })
            : PigPdfDocument.Open(stream);

        return ExtractMetadata(document);
    }

    private static PdfMetadata ExtractMetadata(PigPdfDocument document)
    {
        var info = document.Information;
        return new PdfMetadata
        {
            NumberOfPages = document.NumberOfPages,
            Author = info.Author,
            Title = info.Title,
            Creator = info.Creator,
            Producer = info.Producer,
            Subject = info.Subject,
            Keywords = info.Keywords
        };
    }

    private static List<PageChunks> ExtractFromDocument(PigPdfDocument document)
    {
        var allPageChunks = new List<PageChunks>();

        for (int pageNum = 1; pageNum <= document.NumberOfPages; pageNum++)
        {
            var page = document.GetPage(pageNum);
            var pageChunks = ExtractPageChunks(page, pageNum);
            allPageChunks.Add(pageChunks);
        }

        return allPageChunks;
    }

    private static PageChunks ExtractPageChunks(Page page, int pageNum)
    {
        var pageInfo = new PageInfo
        {
            PageNumber = pageNum,
            Width = page.Width,
            Height = page.Height,
            MediaBoxLeft = page.MediaBox.Bounds.Left,
            MediaBoxBottom = page.MediaBox.Bounds.Bottom,
            MediaBoxRight = page.MediaBox.Bounds.Right,
            MediaBoxTop = page.MediaBox.Bounds.Top,
            CropBoxLeft = page.CropBox.Bounds.Left,
            CropBoxBottom = page.CropBox.Bounds.Bottom,
            CropBoxRight = page.CropBox.Bounds.Right,
            CropBoxTop = page.CropBox.Bounds.Top,
            Rotation = page.Rotation.Value
        };

        var textChunks = ExtractTextChunks(page, pageNum);
        var imageChunks = ExtractImageChunks(page, pageNum);
        var lineChunks = ExtractLineChunks(page, pageNum);

        return new PageChunks
        {
            PageNumber = pageNum,
            PageInfo = pageInfo,
            TextChunks = textChunks,
            ImageChunks = imageChunks,
            LineChunks = lineChunks,
            LineArtChunks = new List<LineArtChunk>()
        };
    }

    private static List<TextChunk> ExtractTextChunks(Page page, int pageNum)
    {
        var chunks = new List<TextChunk>();

        foreach (var letter in page.Letters)
        {
            var bbox = new BoundingBox(
                pageNum,
                letter.GlyphRectangle.Left,
                letter.GlyphRectangle.Bottom,
                letter.GlyphRectangle.Right,
                letter.GlyphRectangle.Top
            );

            var fontName = letter.FontName ?? "Unknown";
            var fontSize = letter.FontSize > 0 ? letter.FontSize : letter.GlyphRectangle.Height;

            // Detect font weight from name heuristics
            double fontWeight = 400.0;
            if (fontName.Contains("Bold", StringComparison.OrdinalIgnoreCase) ||
                fontName.Contains("Black", StringComparison.OrdinalIgnoreCase) ||
                fontName.Contains("Heavy", StringComparison.OrdinalIgnoreCase))
            {
                fontWeight = 700.0;
            }
            else if (fontName.Contains("Light", StringComparison.OrdinalIgnoreCase) ||
                     fontName.Contains("Thin", StringComparison.OrdinalIgnoreCase))
            {
                fontWeight = 300.0;
            }
            else if (fontName.Contains("SemiBold", StringComparison.OrdinalIgnoreCase) ||
                     fontName.Contains("DemiBold", StringComparison.OrdinalIgnoreCase))
            {
                fontWeight = 600.0;
            }
            else if (fontName.Contains("Medium", StringComparison.OrdinalIgnoreCase))
            {
                fontWeight = 500.0;
            }

            bool isItalic = fontName.Contains("Italic", StringComparison.OrdinalIgnoreCase) ||
                            fontName.Contains("Oblique", StringComparison.OrdinalIgnoreCase);

            chunks.Add(new TextChunk
            {
                Value = letter.Value,
                BBox = bbox,
                FontName = fontName,
                FontSize = fontSize,
                FontWeight = fontWeight,
                FontColor = "#000000",
                IsItalic = isItalic,
                SymbolEnds = new List<double> { letter.GlyphRectangle.Right }
            });
        }

        return chunks;
    }

    private static List<ImageChunk> ExtractImageChunks(Page page, int pageNum)
    {
        var images = new List<ImageChunk>();

        foreach (var image in page.GetImages())
        {
            var bounds = image.Bounds;
            images.Add(new ImageChunk
            {
                BBox = new BoundingBox(
                    pageNum,
                    bounds.Left,
                    bounds.Bottom,
                    bounds.Right,
                    bounds.Top
                ),
                Width = bounds.Width,
                Height = bounds.Height,
                ImageData = image.RawBytes.ToArray(),
                MimeType = "image/png"
            });
        }

        return images;
    }

    private static List<LineChunk> ExtractLineChunks(Page page, int pageNum)
    {
        // Extract lines from experimental path data for table border detection.
        // PdfPig's path API varies by version; we use dynamic access for compatibility.
        var lines = new List<LineChunk>();

        try
        {
            var paths = page.ExperimentalAccess.Paths;
            foreach (var path in paths)
            {
                foreach (var subpath in path)
                {
                    // Use the bounding box of each subpath as a potential line
                    var bounds = subpath.GetBoundingRectangle();
                    if (bounds == null) continue;

                    var rect = bounds.Value;
                    double width = rect.Width;
                    double height = rect.Height;

                    // Thin horizontal line
                    if (height < 3.0 && width > 10.0)
                    {
                        double y = rect.Bottom + height / 2;
                        lines.Add(new LineChunk
                        {
                            BBox = new BoundingBox(pageNum, rect.Left, rect.Bottom, rect.Right, rect.Top),
                            StartX = rect.Left,
                            StartY = y,
                            EndX = rect.Right,
                            EndY = y,
                            LineWidth = height
                        });
                    }
                    // Thin vertical line
                    else if (width < 3.0 && height > 10.0)
                    {
                        double x = rect.Left + width / 2;
                        lines.Add(new LineChunk
                        {
                            BBox = new BoundingBox(pageNum, rect.Left, rect.Bottom, rect.Right, rect.Top),
                            StartX = x,
                            StartY = rect.Bottom,
                            EndX = x,
                            EndY = rect.Top,
                            LineWidth = width
                        });
                    }
                }
            }
        }
        catch
        {
            // Path extraction is experimental in PdfPig and may not work for all PDFs
        }

        return lines;
    }
}

public class PdfMetadata
{
    public int NumberOfPages { get; set; }
    public string? Author { get; set; }
    public string? Title { get; set; }
    public string? Creator { get; set; }
    public string? Producer { get; set; }
    public string? Subject { get; set; }
    public string? Keywords { get; set; }
    public string? CreationDate { get; set; }
    public string? ModificationDate { get; set; }
}
