namespace Datapoint.Terminal.Emulator.Screen;

public sealed class ScreenBuffer(int columns, int rows, int maxScrollback, ChangeCounter counter)
{
    private readonly List<Row> _scrollback = [];
    private Row[] _rows = CreateRows(rows, columns, CellStyle.Default);

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

    public void ClearScrollback()
    {
        LinesScrolledOff += _scrollback.Count;
        _scrollback.Clear();
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

        _scrollback.Add(row);
        if (_scrollback.Count <= maxScrollback)
        {
            return;
        }

        _scrollback.RemoveAt(0);
        LinesScrolledOff++;
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
