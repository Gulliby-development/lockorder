using System.Text;

namespace Lockorder.Core;

public static class Table
{
    public static string Render(
        IEnumerable<string> headers,
        IEnumerable<IEnumerable<string>> rows,
        IEnumerable<int>? rightAlignedColumns = null)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);

        string[] headerCells = headers.ToArray();
        if (headerCells.Length == 0)
        {
            throw new ArgumentException("A table must have at least one column.", nameof(headers));
        }

        if (headerCells.Any(static cell => cell is null))
        {
            throw new ArgumentException("Table cells cannot be null.", nameof(headers));
        }

        string[][] rowCells = rows
            .Select(static row => row?.ToArray()
                ?? throw new ArgumentException("Table rows cannot be null.", "rows"))
            .ToArray();

        if (rowCells.Any(row => row.Length != headerCells.Length))
        {
            throw new ArgumentException("Every table row must have the same number of cells as the header.", nameof(rows));
        }

        if (rowCells.SelectMany(static row => row).Any(static cell => cell is null))
        {
            throw new ArgumentException("Table cells cannot be null.", nameof(rows));
        }

        HashSet<int> rightAligned = rightAlignedColumns?.ToHashSet() ?? [];
        if (rightAligned.Any(column => column < 0 || column >= headerCells.Length))
        {
            throw new ArgumentOutOfRangeException(nameof(rightAlignedColumns), "An aligned column index must identify a table column.");
        }

        int[] widths = headerCells.Select(static cell => cell.Length).ToArray();
        foreach (string[] row in rowCells)
        {
            for (int column = 0; column < row.Length; column++)
            {
                widths[column] = Math.Max(widths[column], row[column].Length);
            }
        }

        StringBuilder output = new();
        AppendRow(output, headerCells, widths, rightAligned);

        foreach (string[] row in rowCells)
        {
            output.Append('\n');
            AppendRow(output, row, widths, rightAligned);
        }

        output.Append('\n');
        return output.ToString();
    }

    private static void AppendRow(
        StringBuilder output,
        IReadOnlyList<string> cells,
        IReadOnlyList<int> widths,
        IReadOnlySet<int> rightAlignedColumns)
    {
        for (int column = 0; column < cells.Count; column++)
        {
            if (column > 0)
            {
                output.Append("  ");
            }

            string cell = cells[column];
            int padding = widths[column] - cell.Length;

            if (rightAlignedColumns.Contains(column))
            {
                output.Append(' ', padding);
                output.Append(cell);
            }
            else
            {
                output.Append(cell);
                if (column < cells.Count - 1)
                {
                    output.Append(' ', padding);
                }
            }
        }
    }
}
