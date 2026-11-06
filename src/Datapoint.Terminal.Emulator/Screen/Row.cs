using System.Text;

namespace Datapoint.Terminal.Emulator.Screen;

public sealed class Row
{
    private Cell[] _cells;

    public Row(int columns, CellStyle style)
    {
        _cells = new Cell[columns];
        Erase(0, columns, style);
    }

    public int Columns => _cells.Length;

    public bool IsWrapped { get; set; }

    public long ChangedAt { get; private set; }

    public Cell this[int column] => _cells[column];

    public void Stamp(ChangeCounter counter) => ChangedAt = counter.Next();

    public void Set(int column, Cell cell) => _cells[column] = cell;

    public void AppendToGrapheme(int column, string text)
    {
        var cell = _cells[column];
        cell.Grapheme = cell.Text + text;
        _cells[column] = cell;
    }

    public void Erase(int start, int end, CellStyle style)
    {
        var blank = Cell.Blank(style);
        _cells.AsSpan(start, end - start).Fill(blank);
    }

    public void Insert(int column, int count, int rightMargin, CellStyle style)
    {
        var length = rightMargin - column;
        count = Math.Min(count, length);
        _cells.AsSpan(column, length - count).CopyTo(_cells.AsSpan(column + count));
        Erase(column, column + count, style);
    }

    public void Delete(int column, int count, int rightMargin, CellStyle style)
    {
        var length = rightMargin - column;
        count = Math.Min(count, length);
        _cells.AsSpan(column + count, length - count).CopyTo(_cells.AsSpan(column));
        Erase(rightMargin - count, rightMargin, style);
    }

    public void Resize(int columns, CellStyle style)
    {
        var previous = _cells.Length;
        Array.Resize(ref _cells, columns);
        if (columns <= previous)
        {
            return;
        }

        Erase(previous, columns, style);
    }

    public void CopyFrom(Row other)
    {
        other._cells.AsSpan(0, Math.Min(Columns, other.Columns)).CopyTo(_cells);
        IsWrapped = other.IsWrapped;
    }

    public string GetText()
    {
        var text = new StringBuilder(Columns);
        foreach (var cell in _cells)
        {
            if (cell.IsWideContinuation)
            {
                continue;
            }

            text.Append(cell.CodePoint == 0 ? " " : cell.Text);
        }

        return text.ToString().TrimEnd(' ');
    }
}
