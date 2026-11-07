namespace Datapoint.Terminal.Emulator.Screen;

// Lays lines out again for a new width: rows that wrapped join into one line, which wraps again where the new width ends.
// A wide character never splits across rows.
internal sealed class Reflow(int columns)
{
    private readonly List<Row> _rows = [];

    public IReadOnlyList<Row> Rows => _rows;

    // The new row of each old row, and the cursor's new place.
    public int[] Moved { get; private set; } = [];

    public int CursorRow { get; private set; }

    public int CursorColumn { get; private set; }

    public bool PendingWrap { get; private set; }

    public void Lay(IReadOnlyList<Row> rows, int cursorRow, int cursorColumn, bool pendingWrap)
    {
        Moved = new int[rows.Count];
        var start = 0;
        while (start < rows.Count)
        {
            var end = start;
            while (end < rows.Count - 1 && rows[end].IsWrapped)
            {
                end++;
            }

            var hasCursor = cursorRow >= start && cursorRow <= end;
            LayLine(rows, start, end, hasCursor ? cursorRow - start : -1, cursorColumn, pendingWrap);
            start = end + 1;
        }
    }

    private void LayLine(IReadOnlyList<Row> rows, int start, int end, int cursorRowInLine, int cursorColumn, bool pendingWrap)
    {
        var cells = new List<Cell>();
        var offsets = new int[end - start + 1];
        for (var index = start; index <= end; index++)
        {
            offsets[index - start] = cells.Count;
            var row = rows[index];
            cells.AddRange(row.Cells[..(index == end ? row.ContentLength : row.Columns)]);
        }

        var first = _rows.Count;
        var places = Wrap(cells);
        for (var index = start; index <= end; index++)
        {
            Moved[index] = Math.Min(places[offsets[index - start]].Row, _rows.Count - 1);
        }

        if (cursorRowInLine < 0)
        {
            return;
        }

        PlaceCursor(places, cells.Count, offsets[cursorRowInLine] + cursorColumn, pendingWrap, first);
    }

    // Each cell's new row and column, and one more entry for the place just after the last cell.
    private (int Row, int Column)[] Wrap(List<Cell> cells)
    {
        var places = new (int Row, int Column)[cells.Count + 1];
        var current = new Cell[columns];
        var column = 0;
        for (var index = 0; index < cells.Count; index++)
        {
            var cell = cells[index];
            if (cell.IsWideContinuation)
            {
                places[index] = places[Math.Max(0, index - 1)];
                continue;
            }

            var width = Math.Max(1, (int)cell.Width);
            if (column + width > columns)
            {
                AddRow(current, column, isWrapped: true);
                column = 0;
            }

            places[index] = (_rows.Count, column);
            current[column] = cell;
            if (width == 2)
            {
                current[column + 1] = new Cell { Width = Cell.WideContinuation, Style = cell.Style, HyperlinkId = cell.HyperlinkId };
            }

            column += width;
        }

        places[cells.Count] = (_rows.Count, column);
        AddRow(current, column, isWrapped: false);
        return places;
    }

    private void AddRow(Cell[] cells, int length, bool isWrapped)
    {
        _rows.Add(Row.From(cells.AsSpan(0, length), columns, isWrapped));
        Array.Clear(cells);
    }

    // A cursor waiting to wrap stays on the character it just wrote, unless that character is no longer at the row's end.
    private void PlaceCursor((int Row, int Column)[] places, int length, int offset, bool pendingWrap, int first)
    {
        var (row, column) = offset < length ? places[offset] : (places[length].Row, places[length].Column + (offset - length));
        if (pendingWrap && offset < length)
        {
            PendingWrap = column == columns - 1;
            column = PendingWrap ? column : column + 1;
        }

        row += column / columns;
        column %= columns;
        while (_rows.Count - 1 < row)
        {
            _rows[^1].IsWrapped = true;
            _rows.Add(new Row(columns, CellStyle.Default));
        }

        CursorRow = Math.Max(row, first);
        CursorColumn = column;
    }
}
