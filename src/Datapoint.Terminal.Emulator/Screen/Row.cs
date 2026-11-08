using System.Text;

namespace Datapoint.Terminal.Emulator.Screen;

public sealed class Row
{
    private const int RowOverhead = 48;

    private static readonly int s_cellBytes = System.Runtime.CompilerServices.Unsafe.SizeOf<Cell>();

    private Cell[] _cells;

    public Row(int columns, CellStyle style)
    {
        _cells = new Cell[columns];
        Erase(0, columns, style);
    }

    private Row(Cell[] cells, bool isWrapped)
    {
        _cells = cells;
        IsWrapped = isWrapped;
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

    // What a row costs to keep, roughly: the scrollback is limited by this rather than by its number of lines.
    public long EstimatedBytes => RowOverhead + ((long)_cells.Length * s_cellBytes);

    internal ReadOnlySpan<Cell> Cells => _cells;

    // The cells up to the last one that shows anything: text, a background colour or a link.
    internal int ContentLength
    {
        get
        {
            var length = _cells.Length;
            while (length > 0 && IsBlank(_cells[length - 1]))
            {
                length--;
            }

            return length;
        }
    }

    internal static Row From(ReadOnlySpan<Cell> cells, int columns, bool isWrapped)
    {
        var row = new Row(columns, CellStyle.Default) { IsWrapped = isWrapped };
        cells.CopyTo(row._cells);
        return row;
    }

    // A row leaving the screen keeps only its content, since most lines are much shorter than the window is wide. A wrapped
    // row is full by definition and is kept whole, so reflowing finds its width.
    internal Row Compacted()
    {
        if (IsWrapped)
        {
            return this;
        }

        var length = ContentLength;
        return length == _cells.Length ? this : new Row(_cells.AsSpan(0, length).ToArray(), isWrapped: false);
    }

    private static bool IsBlank(Cell cell) =>
        cell is { CodePoint: 0, Width: 1, Grapheme: null, HyperlinkId: 0 } && cell.Style.Background.Kind == ColorKind.Default;

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
