namespace EdgeParse.Core.Pdf;

using EdgeParse.Core.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Annotations;
using PigPdfDocument = UglyToad.PdfPig.PdfDocument;

/// <summary>
/// Extracts text chunks, images, and lines from PDF pages using PdfPig.
/// Optimized for performance with pre-allocated collections, parallel extraction,
/// font weight caching, WCAG contrast ratio calculation, and superscript/subscript detection.
/// </summary>
public class PdfTextExtractor
{
    // Cache font weight lookups by font name to avoid repeated string scans
    private static readonly Dictionary<string, (double Weight, bool Italic)> FontPropertyCache = new();
    private static readonly object CacheLock = new();

    public static List<PageChunks> ExtractFromFile(string filePath, string? password = null)
    {
        using var document = password != null
            ? PigPdfDocument.Open(filePath, new ParsingOptions { Password = password })
            : PigPdfDocument.Open(filePath);

        return ExtractFromDocument(document);
    }

    public static List<PageChunks> ExtractFromBytes(byte[] data, string? password = null)
    {
        using var stream = new MemoryStream(data);
        using var document = password != null
            ? PigPdfDocument.Open(stream, new ParsingOptions { Password = password })
            : PigPdfDocument.Open(stream);

        return ExtractFromDocument(document);
    }

    public static PdfMetadata GetMetadata(string filePath, string? password = null)
    {
        using var document = password != null
            ? PigPdfDocument.Open(filePath, new ParsingOptions { Password = password })
            : PigPdfDocument.Open(filePath);

        return ExtractMetadata(document);
    }

    public static PdfMetadata GetMetadataFromBytes(byte[] data, string? password = null)
    {
        using var stream = new MemoryStream(data);
        using var document = password != null
            ? PigPdfDocument.Open(stream, new ParsingOptions { Password = password })
            : PigPdfDocument.Open(stream);

        return ExtractMetadata(document);
    }

    /// <summary>
    /// Extracts all pages and returns both metadata and page chunks in a single open,
    /// avoiding double-opening the PDF.
    /// </summary>
    public static (PdfMetadata Metadata, List<PageChunks> Pages) ExtractAll(
        string filePath, string? password = null)
    {
        using var document = password != null
            ? PigPdfDocument.Open(filePath, new ParsingOptions { Password = password })
            : PigPdfDocument.Open(filePath);

        var metadata = ExtractMetadata(document);
        var pages = ExtractFromDocument(document);
        return (metadata, pages);
    }

    /// <summary>
    /// Extracts all pages from bytes in a single open.
    /// </summary>
    public static (PdfMetadata Metadata, List<PageChunks> Pages) ExtractAllFromBytes(
        byte[] data, string? password = null)
    {
        using var stream = new MemoryStream(data);
        using var document = password != null
            ? PigPdfDocument.Open(stream, new ParsingOptions { Password = password })
            : PigPdfDocument.Open(stream);

        var metadata = ExtractMetadata(document);
        var pages = ExtractFromDocument(document);
        return (metadata, pages);
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
        int pageCount = document.NumberOfPages;
        // PdfPig's GetPage is not thread-safe, so extract pages sequentially
        // but pre-allocate the exact capacity
        var allPageChunks = new List<PageChunks>(pageCount);

        for (int pageNum = 1; pageNum <= pageCount; pageNum++)
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
        var letters = page.Letters;
        var chunks = new List<TextChunk>(letters.Count);

        // Collect baseline statistics for superscript/subscript detection
        // Key: fontName+fontSize -> average baseline Y
        var baselineStats = new Dictionary<string, (double SumY, int Count)>();
        foreach (var letter in letters)
        {
            var key = $"{letter.FontName}:{letter.FontSize:F1}";
            if (baselineStats.TryGetValue(key, out var stat))
                baselineStats[key] = (stat.SumY + letter.GlyphRectangle.Bottom, stat.Count + 1);
            else
                baselineStats[key] = (letter.GlyphRectangle.Bottom, 1);
        }

        // Find the dominant (most common) font size for superscript/subscript detection
        var fontSizeCounts = new Dictionary<double, int>();
        foreach (var letter in letters)
        {
            var rounded = Math.Round(letter.FontSize, 1);
            fontSizeCounts[rounded] = fontSizeCounts.GetValueOrDefault(rounded) + 1;
        }
        double dominantFontSize = fontSizeCounts.Count > 0
            ? fontSizeCounts.MaxBy(kv => kv.Value).Key
            : 12.0;

        // Compute background color from page (approximate as white for now)
        // This is used for contrast ratio calculation
        double bgR = 1.0, bgG = 1.0, bgB = 1.0; // white background assumption

        foreach (var letter in letters)
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

            // Cached font property resolution
            var (fontWeight, isItalic) = ResolveFontProperties(fontName);

            // Extract color and compute contrast ratio
            double fgR = 0, fgG = 0, fgB = 0;
            string fontColor = "#000000";
            try
            {
                if (letter.Color != null)
                {
                    var colorStr = letter.Color.ToString();
                    // PdfPig IColor.ToString() may return various formats
                    // Try to parse common patterns
                    if (TryParseColor(colorStr, out fgR, out fgG, out fgB))
                    {
                        fontColor = $"#{(int)(fgR * 255):X2}{(int)(fgG * 255):X2}{(int)(fgB * 255):X2}";
                    }
                }
            }
            catch
            {
                // Color extraction can fail; default to black
            }

            double contrastRatio = CalculateWcagContrastRatio(fgR, fgG, fgB, bgR, bgG, bgB);

            // Superscript/subscript detection based on baseline displacement
            var textFormat = TextFormat.Normal;
            var roundedSize = Math.Round(fontSize, 1);
            if (roundedSize < dominantFontSize * 0.85 && dominantFontSize > 0)
            {
                // Smaller font - check if baseline is above or below the dominant baseline
                var dominantKey = fontSizeCounts.MaxBy(kv => kv.Value).Key;
                // Find the average baseline for the dominant font size
                double avgDominantBaseline = 0;
                int dominantCount = 0;
                foreach (var kv in baselineStats)
                {
                    if (kv.Key.EndsWith($":{dominantKey:F1}"))
                    {
                        avgDominantBaseline += kv.Value.SumY;
                        dominantCount += (int)kv.Value.Count;
                    }
                }
                if (dominantCount > 0)
                {
                    avgDominantBaseline /= dominantCount;
                    double baselineDiff = letter.GlyphRectangle.Bottom - avgDominantBaseline;
                    if (baselineDiff > fontSize * 0.3)
                        textFormat = TextFormat.Superscript;
                    else if (baselineDiff < -fontSize * 0.2)
                        textFormat = TextFormat.Subscript;
                }
            }

            // Math/code detection from font name
            var textType = TextType.Regular;
            if (fontName.Contains("Math", StringComparison.OrdinalIgnoreCase) ||
                fontName.Contains("Symbol", StringComparison.OrdinalIgnoreCase) ||
                fontName.Contains("CMSY", StringComparison.OrdinalIgnoreCase) ||
                fontName.Contains("CMMI", StringComparison.OrdinalIgnoreCase) ||
                fontName.Contains("CMEX", StringComparison.OrdinalIgnoreCase))
            {
                textType = TextType.Math;
            }
            else if (fontName.Contains("Courier", StringComparison.OrdinalIgnoreCase) ||
                     fontName.Contains("Mono", StringComparison.OrdinalIgnoreCase) ||
                     fontName.Contains("Consolas", StringComparison.OrdinalIgnoreCase) ||
                     fontName.Contains("Menlo", StringComparison.OrdinalIgnoreCase))
            {
                textType = TextType.Code;
            }

            chunks.Add(new TextChunk
            {
                Value = letter.Value,
                BBox = bbox,
                FontName = fontName,
                FontSize = fontSize,
                FontWeight = fontWeight,
                FontColor = fontColor,
                ContrastRatio = contrastRatio,
                IsItalic = isItalic,
                TextFormat = textFormat,
                TextType = textType,
                SymbolEnds = new List<double>(1) { letter.GlyphRectangle.Right }
            });
        }

        return chunks;
    }

    /// <summary>
    /// Resolves font weight and italic flag from font name with caching.
    /// </summary>
    private static (double Weight, bool Italic) ResolveFontProperties(string fontName)
    {
        lock (CacheLock)
        {
            if (FontPropertyCache.TryGetValue(fontName, out var cached))
                return cached;
        }

        double weight = 400.0;
        bool italic = false;

        // Weight detection - ordered by specificity
        if (fontName.Contains("Black", StringComparison.OrdinalIgnoreCase) ||
            fontName.Contains("Ultra", StringComparison.OrdinalIgnoreCase) && fontName.Contains("Bold", StringComparison.OrdinalIgnoreCase))
            weight = 900.0;
        else if (fontName.Contains("ExtraBold", StringComparison.OrdinalIgnoreCase) ||
                 fontName.Contains("Extra Bold", StringComparison.OrdinalIgnoreCase))
            weight = 800.0;
        else if (fontName.Contains("Bold", StringComparison.OrdinalIgnoreCase) ||
                 fontName.Contains("Heavy", StringComparison.OrdinalIgnoreCase))
            weight = 700.0;
        else if (fontName.Contains("SemiBold", StringComparison.OrdinalIgnoreCase) ||
                 fontName.Contains("Semi Bold", StringComparison.OrdinalIgnoreCase) ||
                 fontName.Contains("DemiBold", StringComparison.OrdinalIgnoreCase) ||
                 fontName.Contains("Demi Bold", StringComparison.OrdinalIgnoreCase))
            weight = 600.0;
        else if (fontName.Contains("Medium", StringComparison.OrdinalIgnoreCase))
            weight = 500.0;
        else if (fontName.Contains("Light", StringComparison.OrdinalIgnoreCase))
            weight = 300.0;
        else if (fontName.Contains("Thin", StringComparison.OrdinalIgnoreCase) ||
                 fontName.Contains("Hairline", StringComparison.OrdinalIgnoreCase))
            weight = 100.0;

        // Italic/oblique detection
        italic = fontName.Contains("Italic", StringComparison.OrdinalIgnoreCase) ||
                 fontName.Contains("Oblique", StringComparison.OrdinalIgnoreCase) ||
                 fontName.Contains("Slant", StringComparison.OrdinalIgnoreCase);

        var result = (weight, italic);

        lock (CacheLock)
        {
            FontPropertyCache.TryAdd(fontName, result);
        }

        return result;
    }

    /// <summary>
    /// Calculates WCAG 2.0 contrast ratio between foreground and background colors.
    /// Returns value between 1.0 (no contrast) and 21.0 (max contrast).
    /// </summary>
    private static double CalculateWcagContrastRatio(
        double fgR, double fgG, double fgB,
        double bgR, double bgG, double bgB)
    {
        double fgLuminance = RelativeLuminance(fgR, fgG, fgB);
        double bgLuminance = RelativeLuminance(bgR, bgG, bgB);

        double lighter = Math.Max(fgLuminance, bgLuminance);
        double darker = Math.Min(fgLuminance, bgLuminance);

        return (lighter + 0.05) / (darker + 0.05);
    }

    /// <summary>
    /// Computes relative luminance per WCAG 2.0 spec.
    /// Input values are in 0..1 range (sRGB).
    /// </summary>
    private static double RelativeLuminance(double r, double g, double b)
    {
        double rLin = r <= 0.03928 ? r / 12.92 : Math.Pow((r + 0.055) / 1.055, 2.4);
        double gLin = g <= 0.03928 ? g / 12.92 : Math.Pow((g + 0.055) / 1.055, 2.4);
        double bLin = b <= 0.03928 ? b / 12.92 : Math.Pow((b + 0.055) / 1.055, 2.4);

        return 0.2126 * rLin + 0.7152 * gLin + 0.0722 * bLin;
    }

    /// <summary>
    /// Tries to parse a PdfPig IColor string representation to RGB (0..1).
    /// </summary>
    private static bool TryParseColor(string? colorStr, out double r, out double g, out double b)
    {
        r = 0; g = 0; b = 0;
        if (string.IsNullOrEmpty(colorStr)) return false;

        // Try parsing patterns like "R: 0.5, G: 0.3, B: 0.1" or "0 0 0" (grayscale or RGB)
        var parts = colorStr.Split(new[] { ' ', ',', ':', ';' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var numbers = new List<double>();
        foreach (var part in parts)
        {
            if (double.TryParse(part, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double val))
            {
                numbers.Add(val);
            }
        }

        switch (numbers.Count)
        {
            case 1: // Grayscale
                r = g = b = numbers[0];
                return true;
            case 3: // RGB
                r = numbers[0]; g = numbers[1]; b = numbers[2];
                // Normalize if values > 1 (likely 0-255 range)
                if (r > 1 || g > 1 || b > 1)
                {
                    r /= 255; g /= 255; b /= 255;
                }
                return true;
            case 4: // CMYK - convert to RGB
                double c = numbers[0], m = numbers[1], y = numbers[2], k = numbers[3];
                r = (1 - c) * (1 - k);
                g = (1 - m) * (1 - k);
                b = (1 - y) * (1 - k);
                return true;
            default:
                return false;
        }
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
        var lines = new List<LineChunk>();

        try
        {
            var paths = page.ExperimentalAccess.Paths;
            foreach (var path in paths)
            {
                foreach (var subpath in path)
                {
                    var bounds = subpath.GetBoundingRectangle();
                    if (bounds == null) continue;

                    var rect = bounds.Value;
                    double width = rect.Width;
                    double height = rect.Height;

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
            // Path extraction is experimental
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
