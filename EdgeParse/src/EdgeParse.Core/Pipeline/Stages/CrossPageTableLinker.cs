namespace EdgeParse.Core.Pipeline.Stages;

using EdgeParse.Core.Models;

/// <summary>
/// Stage 15: Links tables that span multiple pages via previous_table/next_table chains.
/// </summary>
public static class CrossPageTableLinker
{
    private const double ColumnMatchTolerance = 10.0; // pt

    public static List<ContentElement> LinkCrossPageTables(List<ContentElement> elements)
    {
        var tables = elements
            .Select((e, idx) => (Element: e, Index: idx))
            .Where(x => x.Element.Type == ContentElementType.Table ||
                        x.Element.Type == ContentElementType.TableBorder)
            .ToList();

        if (tables.Count < 2) return elements;

        for (int i = 0; i < tables.Count - 1; i++)
        {
            var current = tables[i];
            var next = tables[i + 1];

            var currentTable = current.Element.Type == ContentElementType.Table
                ? current.Element.Table!.Table
                : current.Element.TableBorder!;

            var nextTable = next.Element.Type == ContentElementType.Table
                ? next.Element.Table!.Table
                : next.Element.TableBorder!;

            // Check if tables are on consecutive pages
            if (nextTable.BBox.PageNumber != currentTable.BBox.PageNumber + 1)
                continue;

            // Check if column structure matches
            if (currentTable.NumColumns != nextTable.NumColumns)
                continue;

            // Check if column X-coordinates match
            bool columnsMatch = true;
            for (int c = 0; c < Math.Min(currentTable.XCoordinates.Count, nextTable.XCoordinates.Count); c++)
            {
                if (Math.Abs(currentTable.XCoordinates[c] - nextTable.XCoordinates[c]) > ColumnMatchTolerance)
                {
                    columnsMatch = false;
                    break;
                }
            }

            if (columnsMatch)
            {
                currentTable.NextTable = nextTable;
                nextTable.PreviousTable = currentTable;
            }
        }

        return elements;
    }
}
