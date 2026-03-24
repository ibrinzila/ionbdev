namespace EdgeParse.Core.Pipeline;

using EdgeParse.Core.Api;
using EdgeParse.Core.Models;
using EdgeParse.Core.Pdf;
using EdgeParse.Core.Pipeline.Stages;
using EdgeParse.Core.Utils;

/// <summary>
/// 20-stage processing pipeline that transforms raw PDF chunks into semantic document elements.
/// Mirrors the Rust orchestrator.rs stage sequence.
/// </summary>
public class Orchestrator
{
    private readonly ProcessingConfig _config;

    public Orchestrator(ProcessingConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Runs the full pipeline on extracted page chunks and returns a PdfDocument.
    /// </summary>
    public PdfDocument Process(
        List<PageChunks> allPageChunks,
        PdfMetadata metadata,
        string fileName)
    {
        // Stage 0b: Page range filtering
        var pageSet = _config.ParsePageSet();
        if (pageSet != null)
        {
            allPageChunks = allPageChunks
                .Where(p => pageSet.Contains(p.PageNumber))
                .ToList();
        }

        // Stage 1b: Watermark removal
        allPageChunks = WatermarkRemover.RemoveWatermarks(allPageChunks);

        // Stage 2: Content filtering
        allPageChunks = ContentFilter.Filter(allPageChunks, _config.FilterConfig);

        // Build page info lookup
        var pageInfos = allPageChunks.ToDictionary(p => p.PageNumber, p => p.PageInfo);

        // Per-page processing stages
        var allElements = new List<ContentElement>();

        foreach (var pageChunks in allPageChunks)
        {
            var pageElements = ProcessPage(pageChunks);
            allElements.AddRange(pageElements);
        }

        // Stage 8: Header/footer detection (cross-page)
        allElements = HeaderFooterDetector.DetectHeaderFooters(allElements, pageInfos);

        // Stage 9 + 11: List detection
        allElements = ListDetector.DetectLists(allElements);

        // Stage 12: Heading detection
        allElements = HeadingDetector.DetectHeadings(allElements);

        // Stage 13: ID assignment
        AssignIndices(allElements);

        // Stage 14: Caption linking
        allElements = CaptionLinker.LinkCaptions(allElements);

        // Stage 15: Cross-page table linking
        allElements = CrossPageTableLinker.LinkCrossPageTables(allElements);

        // Stage 18: Reading order sort
        if (_config.ReadingOrder == ReadingOrder.XyCut)
        {
            allElements = XyCut.Sort(allElements);
            // Re-assign indices after sorting
            AssignIndices(allElements);
        }

        // Remove header/footer if not configured to include
        if (!_config.IncludeHeaderFooter)
        {
            allElements = allElements
                .Where(e => e.Type != ContentElementType.HeaderFooter)
                .ToList();
        }

        return new PdfDocument
        {
            FileName = fileName,
            NumberOfPages = metadata.NumberOfPages,
            Author = metadata.Author,
            Title = metadata.Title,
            CreationDate = metadata.CreationDate,
            ModificationDate = metadata.ModificationDate,
            Producer = metadata.Producer,
            Creator = metadata.Creator,
            Subject = metadata.Subject,
            Keywords = metadata.Keywords,
            Kids = allElements
        };
    }

    private List<ContentElement> ProcessPage(PageChunks pageChunks)
    {
        var elements = new List<ContentElement>();

        // Stage 3-4: Table detection from ruling lines
        var tables = new List<TableBorder>();
        var remainingTextChunks = pageChunks.TextChunks;

        if (pageChunks.LineChunks.Count >= 4)
        {
            var (detectedTables, remaining) = TableDetector.DetectTables(
                pageChunks.LineChunks, pageChunks.TextChunks, pageChunks.PageInfo);
            tables = detectedTables;
            remainingTextChunks = remaining;
        }

        // Stage 5b: Column detection
        var columnLayout = ColumnDetector.DetectColumns(remainingTextChunks, pageChunks.PageInfo);

        // Stage 6: TextChunk → TextLine grouping
        var textLines = TextLineGrouper.GroupIntoLines(remainingTextChunks, columnLayout);

        // Stage 6b: Re-run column detection on lines (more stable)
        columnLayout = ColumnDetector.DetectColumnsFromLines(textLines, pageChunks.PageInfo);

        // Stage 7: TextLine → TextBlock grouping
        var textBlocks = TextBlockGrouper.GroupIntoBlocks(textLines);

        // Stage 7b: Cluster table detection (borderless tables)
        if (_config.TableMethod == TableMethod.Cluster)
        {
            var (clusterTables, remainingBlocks) = TableDetector.DetectClusterTables(
                textBlocks, pageChunks.PageInfo);
            foreach (var ct in clusterTables)
                tables.Add(ct);
            textBlocks = remainingBlocks;
        }

        // Add tables as elements
        foreach (var table in tables)
        {
            var semanticTable = new SemanticTable { Table = table };
            elements.Add(ContentElement.FromTable(semanticTable));
        }

        // Stage 10: Convert TextBlocks to Paragraphs
        foreach (var block in textBlocks)
        {
            elements.Add(ContentElement.FromTextBlock(block));
        }
        elements = ParagraphDetector.ConvertToParagraphs(elements);

        // Stage 10b: Figure detection
        foreach (var image in pageChunks.ImageChunks)
        {
            elements.Add(ContentElement.FromImage(image));
        }
        foreach (var lineArt in pageChunks.LineArtChunks)
        {
            elements.Add(ContentElement.FromLineArt(lineArt));
        }
        elements = FigureDetector.DetectFigures(elements);

        return elements;
    }

    private static void AssignIndices(List<ContentElement> elements)
    {
        for (int i = 0; i < elements.Count; i++)
        {
            SetIndex(elements[i], i);
        }
    }

    private static void SetIndex(ContentElement elem, int index)
    {
        switch (elem.Type)
        {
            case ContentElementType.TextLine:
                elem.TextLine!.Index = index;
                break;
            case ContentElementType.TextBlock:
                elem.TextBlock!.Index = index;
                break;
            case ContentElementType.Image:
                elem.Image!.Index = index;
                break;
            case ContentElementType.LineArt:
                elem.LineArt!.Index = index;
                break;
            case ContentElementType.Paragraph:
                elem.Paragraph!.Index = index;
                break;
            case ContentElementType.Heading:
                elem.Heading!.Index = index;
                break;
            case ContentElementType.NumberHeading:
                elem.NumberHeading!.Index = index;
                break;
            case ContentElementType.Table:
                elem.Table!.Index = index;
                break;
            case ContentElementType.List:
                elem.List!.Index = index;
                break;
            case ContentElementType.Caption:
                elem.Caption!.Index = index;
                break;
            case ContentElementType.Figure:
                elem.Figure!.Index = index;
                break;
            case ContentElementType.Formula:
                elem.Formula!.Index = index;
                break;
            case ContentElementType.HeaderFooter:
                elem.HeaderFooter!.Index = index;
                break;
            case ContentElementType.TableBorder:
                elem.TableBorder!.Index = index;
                break;
        }
    }
}
