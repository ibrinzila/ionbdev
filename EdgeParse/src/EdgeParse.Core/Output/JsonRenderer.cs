namespace EdgeParse.Core.Output;

using EdgeParse.Core.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Renders a PdfDocument as structured JSON with full bounding box information.
/// </summary>
public static class JsonRenderer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    public static string Render(PdfDocument doc)
    {
        var output = new JsonDocument
        {
            FileName = doc.FileName,
            NumberOfPages = doc.NumberOfPages,
            Metadata = new JsonMetadata
            {
                Author = doc.Author,
                Title = doc.Title,
                CreationDate = doc.CreationDate,
                ModificationDate = doc.ModificationDate,
                Producer = doc.Producer,
                Creator = doc.Creator,
                Subject = doc.Subject,
                Keywords = doc.Keywords
            },
            Elements = doc.Kids.Select(RenderElement).ToList()
        };

        return JsonSerializer.Serialize(output, Options);
    }

    private static JsonElement RenderElement(ContentElement element)
    {
        return new JsonElement
        {
            Type = element.Type.ToString(),
            Index = element.Index,
            PageNumber = element.PageNumber,
            BoundingBox = RenderBBox(element.BBox),
            Text = element.TextValue,
            Heading = element.Type == ContentElementType.Heading
                ? new JsonHeadingInfo { Level = element.Heading!.HeadingLevel }
                : null,
            Table = element.Type == ContentElementType.Table
                ? RenderTable(element.Table!)
                : null,
            List = element.Type == ContentElementType.List
                ? RenderList(element.List!)
                : null
        };
    }

    private static JsonBoundingBox RenderBBox(BoundingBox bbox)
    {
        return new JsonBoundingBox
        {
            PageNumber = bbox.PageNumber,
            LeftX = Math.Round(bbox.LeftX, 2),
            BottomY = Math.Round(bbox.BottomY, 2),
            RightX = Math.Round(bbox.RightX, 2),
            TopY = Math.Round(bbox.TopY, 2)
        };
    }

    private static JsonTableInfo RenderTable(SemanticTable table)
    {
        return new JsonTableInfo
        {
            NumRows = table.Table.NumRows,
            NumColumns = table.Table.NumColumns,
            Rows = table.Table.Rows.Select(r => new JsonTableRow
            {
                Cells = r.Cells.Select(c => new JsonTableCell
                {
                    RowNumber = c.RowNumber,
                    ColNumber = c.ColNumber,
                    RowSpan = c.RowSpan,
                    ColSpan = c.ColSpan,
                    Text = c.TextContent,
                    BoundingBox = RenderBBox(c.BBox)
                }).ToList()
            }).ToList()
        };
    }

    private static JsonListInfo RenderList(PdfList list)
    {
        return new JsonListInfo
        {
            ListType = list.ListTypeValue.ToString(),
            Items = list.Items.Select(i => new JsonListItem
            {
                Label = i.Label.Value,
                Text = i.Body.Value()
            }).ToList()
        };
    }

    // JSON output models
    private class JsonDocument
    {
        public string FileName { get; set; } = string.Empty;
        public int NumberOfPages { get; set; }
        public JsonMetadata? Metadata { get; set; }
        public List<JsonElement> Elements { get; set; } = new();
    }

    private class JsonMetadata
    {
        public string? Author { get; set; }
        public string? Title { get; set; }
        public string? CreationDate { get; set; }
        public string? ModificationDate { get; set; }
        public string? Producer { get; set; }
        public string? Creator { get; set; }
        public string? Subject { get; set; }
        public string? Keywords { get; set; }
    }

    private class JsonElement
    {
        public string Type { get; set; } = string.Empty;
        public int? Index { get; set; }
        public int PageNumber { get; set; }
        public JsonBoundingBox? BoundingBox { get; set; }
        public string? Text { get; set; }
        public JsonHeadingInfo? Heading { get; set; }
        public JsonTableInfo? Table { get; set; }
        public JsonListInfo? List { get; set; }
    }

    private class JsonBoundingBox
    {
        public int PageNumber { get; set; }
        public double LeftX { get; set; }
        public double BottomY { get; set; }
        public double RightX { get; set; }
        public double TopY { get; set; }
    }

    private class JsonHeadingInfo
    {
        public int? Level { get; set; }
    }

    private class JsonTableInfo
    {
        public int NumRows { get; set; }
        public int NumColumns { get; set; }
        public List<JsonTableRow> Rows { get; set; } = new();
    }

    private class JsonTableRow
    {
        public List<JsonTableCell> Cells { get; set; } = new();
    }

    private class JsonTableCell
    {
        public int RowNumber { get; set; }
        public int ColNumber { get; set; }
        public int RowSpan { get; set; }
        public int ColSpan { get; set; }
        public string Text { get; set; } = string.Empty;
        public JsonBoundingBox? BoundingBox { get; set; }
    }

    private class JsonListInfo
    {
        public string ListType { get; set; } = string.Empty;
        public List<JsonListItem> Items { get; set; } = new();
    }

    private class JsonListItem
    {
        public string Label { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }
}
