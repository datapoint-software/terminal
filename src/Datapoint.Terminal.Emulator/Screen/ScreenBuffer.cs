namespace Datapoint.Terminal.Emulator.Screen;

// The scrollback is limited both in lines and in an estimate of the memory its rows take, whichever is reached first.
public sealed class ScreenBuffer(int columns, int rows, int maxScrollback, ChangeCounter counter, long maxScrollbackBytes = ScreenBuffer.DefaultScrollbackBytes)
{
    public const long DefaultScrollbackBytes = 64L * 1024 * 1024;

    private readonly List<Row> _scrollback = [];
    private Row[] _rows = CreateRows(rows, columns, CellStyle.Default);
    private long _scrollbackBytes;

    public int Columns { get; private set; } = columns;

    public int Rows => _rows.Length;

    public IReadOnlyList<Row> Scrollback => _scrollback;

    public long LinesScrolledOff { get; private set; }

    public long FirstVisibleLine => LinesScrolledOff + _scrollback.Count;

    public Row this[int row] => _rows[row];

    public void Touch(int row) => _rows[row].Stamp(counter);

    public void ScrollUp(int top, int bottom, int count, CellStyle style)
    {
        count = Math.Min(count, bottom - top + 1);
        for (var step = 0; step < count; step++)
        {
            var leaving = _rows[top];
            KeepInScrollback(leaving, top);
            Array.Copy(_rows, top + 1, _rows, top, bottom - top);
            _rows[bottom] = new Row(Columns, style);
        }

        TouchRange(top, bottom);
    }

    public void ScrollUpWithoutScrollback(int top, int bottom, int count, CellStyle style)
    {
        count = Math.Min(count, bottom - top + 1);
        for (var step = 0; step < count; step++)
        {
            Array.Copy(_rows, top + 1, _rows, top, bottom - top);
            _rows[bottom] = new Row(Columns, style);
        }

        TouchRange(top, bottom);
    }

    public void ScrollDown(int top, int bottom, int count, CellStyle style)
    {
        count = Math.Min(count, bottom - top + 1);
        for (var step = 0; step < count; step++)
        {
            Array.Copy(_rows, top, _rows, top + 1, bottom - top);
            _rows[top] = new Row(Columns, style);
        }

        TouchRange(top, bottom);
    }

    public void EraseRows(int top, int bottom, CellStyle style)
    {
        for (var row = top; row <= bottom; row++)
        {
            _rows[row].Erase(0, Columns, style);
            _rows[row].IsWrapped = false;
        }

        TouchRange(top, bottom);
    }

    public long ScrollbackBytes => _scrollbackBytes;

    public void ClearScrollback()
    {
        LinesScrolledOff += _scrollback.Count;
        _scrollback.Clear();
        _scrollbackBytes = 0;
    }

    // Lays the scrollback and screen out again for a new width, keeping the cursor on the same character. Blank rows below the
    // cursor go first; the rest fill the screen from the bottom, and what doesn't fit goes to the scrollback.
    public LineMap Reflow(int newColumns, int newRows, Cursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        var reflow = new Reflow(newColumns);
        reflow.Lay([.. _scrollback, .. _rows], _scrollback.Count + cursor.Row, cursor.Column, cursor.PendingWrap);
        var laid = reflow.Rows.ToList();
        while (laid.Count - 1 > reflow.CursorRow && laid[^1] is { IsWrapped: false, ContentLength: 0 })
        {
            laid.RemoveAt(laid.Count - 1);
        }

        var top = Math.Clamp(laid.Count - newRows, 0, reflow.CursorRow);
        var first = LinesScrolledOff;
        _scrollback.Clear();
        _scrollbackBytes = 0;
        foreach (var row in laid.Take(top))
        {
            AddToScrollback(row);
        }

        _rows = CreateRows(newRows, newColumns, CellStyle.Default);
        for (var row = 0; row < newRows && top + row < laid.Count; row++)
        {
            _rows[row] = laid[top + row];
        }

        Columns = newColumns;
        cursor.Row = reflow.CursorRow - top;
        cursor.Column = reflow.CursorColumn;
        cursor.PendingWrap = reflow.PendingWrap;
        TouchRange(0, newRows - 1);
        return new LineMap(first, reflow.Moved, first + top + newRows - 1);
    }

    public void Resize(int newColumns, int newRows, int dropFromTop)
    {
        var resized = CreateRows(newRows, newColumns, CellStyle.Default);
        var dropped = Math.Clamp(dropFromTop, 0, _rows.Length);
        var keep = Math.Min(newRows, _rows.Length - dropped);
        for (var row = 0; row < dropped; row++)
        {
            KeepInScrollback(_rows[row], 0);
        }

        for (var row = 0; row < keep; row++)
        {
            resized[row].CopyFrom(_rows[dropped + row]);
        }

        Columns = newColumns;
        _rows = resized;
        TouchRange(0, newRows - 1);
    }

    private void KeepInScrollback(Row row, int top)
    {
        if (maxScrollback == 0 || top != 0)
        {
            return;
        }

        AddToScrollback(row);
    }

    private void AddToScrollback(Row row)
    {
        if (maxScrollback == 0)
        {
            return;
        }

        var kept = row.Compacted();
        _scrollback.Add(kept);
        _scrollbackBytes += kept.EstimatedBytes;
        var drop = 0;
        while (_scrollback.Count - drop > 1 && (_scrollback.Count - drop > maxScrollback || _scrollbackBytes > maxScrollbackBytes))
        {
            _scrollbackBytes -= _scrollback[drop].EstimatedBytes;
            drop++;
        }

        // Removed in one go: taking rows off the front one at a time moves the whole list each time.
        _scrollback.RemoveRange(0, drop);
        LinesScrolledOff += drop;
    }

    private void TouchRange(int top, int bottom)
    {
        for (var row = top; row <= bottom; row++)
        {
            _rows[row].Stamp(counter);
        }
    }

    private static Row[] CreateRows(int rowCount, int columnCount, CellStyle style)
    {
        var created = new Row[rowCount];
        for (var row = 0; row < rowCount; row++)
        {
            created[row] = new Row(columnCount, style);
        }

        return created;
    }
}
