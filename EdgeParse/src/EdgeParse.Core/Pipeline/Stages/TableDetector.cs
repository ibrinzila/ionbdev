namespace EdgeParse.Core.Pipeline.Stages;

using EdgeParse.Core.Models;

/// <summary>
/// Stages 3-4: Detects tables from ruling lines (horizontal/vertical lines forming grids).
/// Builds TableBorder structures from line intersections and assigns content to cells.
/// </summary>
public static class TableDetector
{
    private const double CoordinateEpsilon = 0.5; // pt tolerance for line clustering
    private const double MaxPageCoverageRatio = 0.9; // Reject tables covering > 90% of page
    private const double MinIntersectionRatio = 0.01; // 1% overlap for cell content assignment
    private const double MinGutterWidth = 8.0; // pt - minimum column width before merging

    /// <summary>
    /// Detects tables from line chunks and assigns text content to cells.
    /// Returns detected tables and remaining (unassigned) text chunks.
    /// </summary>
    public static (List<TableBorder> Tables, List<TextChunk> RemainingChunks)
        DetectTables(List<LineChunk> lines, List<TextChunk> textChunks, PageInfo pageInfo)
    {
        var tables = new List<TableBorder>();

        if (lines.Count < 4) // Need at least 2 horizontal + 2 vertical lines
            return (tables, textChunks);

        var horizontalLines = lines.Where(l => l.IsHorizontal).ToList();
        var verticalLines = lines.Where(l => l.IsVertical).ToList();

        if (horizontalLines.Count < 2 || verticalLines.Count < 2)
            return (tables, textChunks);

        // Cluster horizontal line Y-coordinates
        var yCoords = ClusterCoordinates(horizontalLines.Select(l => l.StartY).ToList());
        // Cluster vertical line X-coordinates
        var xCoords = ClusterCoordinates(verticalLines.Select(l => l.StartX).ToList());

        if (yCoords.Count < 2 || xCoords.Count < 2)
            return (tables, textChunks);

        xCoords.Sort();
        yCoords.Sort(); // ascending (bottom to top in PDF space)
        yCoords.Reverse(); // descending (top to bottom for row order)

        // Build the table grid
        var table = BuildTableGrid(xCoords, yCoords, pageInfo);
        if (table == null)
            return (tables, textChunks);

        // Reject tables covering most of the page (layout artifacts)
        double pageArea = pageInfo.Width * pageInfo.Height;
        if (pageArea > 0 && table.BBox.Area / pageArea > MaxPageCoverageRatio)
            return (tables, textChunks);

        // Assign text content to cells
        var assignedChunks = new HashSet<int>();
        AssignContentToCells(table, textChunks, assignedChunks);

        // Filter out empty tables
        int totalCellsWithContent = table.Rows.Sum(r => r.Cells.Count(c => c.Content.Count > 0));
        int totalCells = table.Rows.Sum(r => r.Cells.Count);
        if (totalCells > 0 && (double)totalCellsWithContent / totalCells < 0.1)
            return (tables, textChunks); // Less than 10% cells have content - likely not a table

        // Detect cell spans (merged cells)
        DetectCellSpans(table);

        tables.Add(table);

        // Remove assigned chunks from remaining
        var remaining = textChunks
            .Where((_, idx) => !assignedChunks.Contains(idx))
            .ToList();

        return (tables, remaining);
    }

    /// <summary>
    /// Detects rowspan/colspan by finding empty cells adjacent to cells with content
    /// that spans across multiple grid positions based on ruling line gaps.
    /// </summary>
    private static void DetectCellSpans(TableBorder table)
    {
        // Detect colspan: if a horizontal line is missing between two cells,
        // they may be a single merged cell
        for (int r = 0; r < table.Rows.Count; r++)
        {
            var row = table.Rows[r];
            for (int c = 0; c < row.Cells.Count - 1; c++)
            {
                var cell = row.Cells[c];
                var nextCell = row.Cells[c + 1];

                // If current cell has content and next cell is empty,
                // and the content bbox extends into the next cell
                if (cell.Content.Count > 0 && nextCell.Content.Count == 0)
                {
                    cell.ColSpan++;
                    // Expand cell bbox to include the spanned cell
                    cell.BBox = cell.BBox.Union(nextCell.BBox);
                }
            }
        }

        // Detect rowspan: if a vertical line is missing between two cells
        for (int c = 0; c < table.NumColumns; c++)
        {
            for (int r = 0; r < table.Rows.Count - 1; r++)
            {
                if (c >= table.Rows[r].Cells.Count || c >= table.Rows[r + 1].Cells.Count)
                    continue;

                var cell = table.Rows[r].Cells[c];
                var belowCell = table.Rows[r + 1].Cells[c];

                if (cell.Content.Count > 0 && belowCell.Content.Count == 0)
                {
                    cell.RowSpan++;
                    cell.BBox = cell.BBox.Union(belowCell.BBox);
                }
            }
        }
    }

    /// <summary>
    /// Detects tables using cluster method (for borderless tables).
    /// Groups TextBlocks with regular X/Y spacing into table structures.
    /// </summary>
    public static (List<TableBorder> Tables, List<TextBlock> RemainingBlocks)
        DetectClusterTables(List<TextBlock> blocks, PageInfo pageInfo)
    {
        var tables = new List<TableBorder>();

        if (blocks.Count < 4)
            return (tables, blocks);

        // Group blocks by Y position (potential rows)
        var rows = GroupByY(blocks);
        if (rows.Count < 2)
            return (tables, blocks);

        // Check if rows have consistent number of elements (column structure)
        var columnCounts = rows.Select(r => r.Count).ToList();
        int modeColumnCount = columnCounts
            .GroupBy(c => c)
            .OrderByDescending(g => g.Count())
            .First().Key;

        if (modeColumnCount < 2)
            return (tables, blocks);

        // Only use rows matching the mode column count
        var tableRows = rows.Where(r => r.Count == modeColumnCount).ToList();
        if (tableRows.Count < 2)
            return (tables, blocks);

        // Build X coordinates from block positions
        var xCoords = new List<double>();
        var allBlocks = tableRows.SelectMany(r => r).OrderBy(b => b.BBox.LeftX).ToList();

        // Get column boundaries from average positions
        for (int col = 0; col < modeColumnCount; col++)
        {
            var colBlocks = tableRows.Select(r => r.OrderBy(b => b.BBox.LeftX).ElementAtOrDefault(col))
                .Where(b => b != null).ToList();

            if (col == 0)
                xCoords.Add(colBlocks.Min(b => b!.BBox.LeftX));

            xCoords.Add(colBlocks.Max(b => b!.BBox.RightX));
        }

        // Build Y coordinates
        var yCoords = new List<double>();
        foreach (var row in tableRows)
        {
            yCoords.Add(row.Max(b => b.BBox.TopY));
        }
        yCoords.Add(tableRows.Last().Min(b => b.BBox.BottomY));

        xCoords = xCoords.Distinct().OrderBy(x => x).ToList();
        yCoords = yCoords.Distinct().OrderByDescending(y => y).ToList();

        if (xCoords.Count < 2 || yCoords.Count < 2)
            return (tables, blocks);

        var table = BuildTableGrid(xCoords, yCoords, pageInfo);
        if (table != null)
        {
            // Assign block content to cells
            var assignedBlocks = new HashSet<int>();
            for (int ri = 0; ri < tableRows.Count; ri++)
            {
                var row = tableRows[ri].OrderBy(b => b.BBox.LeftX).ToList();
                for (int ci = 0; ci < row.Count && ci < table.Rows[ri].Cells.Count; ci++)
                {
                    table.Rows[ri].Cells[ci].Content.Add(row[ci].Value());
                    int blockIdx = blocks.IndexOf(row[ci]);
                    if (blockIdx >= 0) assignedBlocks.Add(blockIdx);
                }
            }

            tables.Add(table);
            var remaining = blocks.Where((_, idx) => !assignedBlocks.Contains(idx)).ToList();
            return (tables, remaining);
        }

        return (tables, blocks);
    }

    private static List<double> ClusterCoordinates(List<double> coords)
    {
        if (coords.Count == 0) return new List<double>();

        coords.Sort();
        var clusters = new List<double>();
        double current = coords[0];
        int count = 1;

        for (int i = 1; i < coords.Count; i++)
        {
            if (Math.Abs(coords[i] - current / count) < CoordinateEpsilon)
            {
                current += coords[i];
                count++;
            }
            else
            {
                clusters.Add(current / count);
                current = coords[i];
                count = 1;
            }
        }
        clusters.Add(current / count);

        return clusters;
    }

    private static TableBorder? BuildTableGrid(List<double> xCoords, List<double> yCoords, PageInfo pageInfo)
    {
        int numCols = xCoords.Count - 1;
        int numRows = yCoords.Count - 1;

        if (numCols < 1 || numRows < 1) return null;

        // Merge gutter columns (too narrow)
        var mergedXCoords = new List<double> { xCoords[0] };
        for (int i = 1; i < xCoords.Count; i++)
        {
            if (xCoords[i] - mergedXCoords.Last() < MinGutterWidth && i < xCoords.Count - 1)
            {
                // Skip this coordinate (merge with next)
                continue;
            }
            mergedXCoords.Add(xCoords[i]);
        }
        xCoords = mergedXCoords;
        numCols = xCoords.Count - 1;
        if (numCols < 1) return null;

        var rows = new List<TableBorderRow>();
        for (int r = 0; r < numRows; r++)
        {
            var cells = new List<TableBorderCell>();
            for (int c = 0; c < numCols; c++)
            {
                cells.Add(new TableBorderCell
                {
                    BBox = new BoundingBox(
                        pageInfo.PageNumber,
                        xCoords[c],
                        yCoords[r + 1], // bottom (remember yCoords are descending)
                        xCoords[c + 1],
                        yCoords[r]       // top
                    ),
                    RowNumber = r,
                    ColNumber = c
                });
            }
            rows.Add(new TableBorderRow { Cells = cells, RowNumber = r });
        }

        return new TableBorder
        {
            BBox = new BoundingBox(
                pageInfo.PageNumber,
                xCoords.First(),
                yCoords.Last(),
                xCoords.Last(),
                yCoords.First()
            ),
            XCoordinates = xCoords,
            YCoordinates = yCoords,
            Rows = rows,
            NumRows = numRows,
            NumColumns = numCols
        };
    }

    private static void AssignContentToCells(
        TableBorder table, List<TextChunk> chunks, HashSet<int> assignedChunks)
    {
        for (int idx = 0; idx < chunks.Count; idx++)
        {
            var chunk = chunks[idx];
            foreach (var row in table.Rows)
            {
                foreach (var cell in row.Cells)
                {
                    if (cell.BBox.Overlaps(chunk.BBox, MinIntersectionRatio))
                    {
                        cell.Content.Add(chunk.Value);
                        assignedChunks.Add(idx);
                        break; // assign to first matching cell
                    }
                }
                if (assignedChunks.Contains(idx)) break;
            }
        }
    }

    private static List<List<TextBlock>> GroupByY(List<TextBlock> blocks)
    {
        if (blocks.Count == 0) return new List<List<TextBlock>>();

        var sorted = blocks.OrderByDescending(b => b.BBox.CenterY).ToList();
        var rows = new List<List<TextBlock>>();
        var currentRow = new List<TextBlock> { sorted[0] };
        double currentY = sorted[0].BBox.CenterY;

        for (int i = 1; i < sorted.Count; i++)
        {
            double tolerance = sorted[i].BBox.Height * 0.5;
            if (Math.Abs(sorted[i].BBox.CenterY - currentY) <= tolerance)
            {
                currentRow.Add(sorted[i]);
            }
            else
            {
                rows.Add(currentRow);
                currentRow = new List<TextBlock> { sorted[i] };
                currentY = sorted[i].BBox.CenterY;
            }
        }
        if (currentRow.Count > 0) rows.Add(currentRow);

        return rows;
    }
}
