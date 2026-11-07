using System.Text;
using Datapoint.Terminal.Emulator.Parsing;
using Datapoint.Terminal.Emulator.Screen;
using Datapoint.Terminal.Emulator.Unicode;

namespace Datapoint.Terminal.Emulator;

public sealed partial class Terminal : IParserHandler
{
    public const int DefaultScrollback = 100_000;
    public const int MaxTitleLength = 1024;

    private readonly Parser _parser;
    private readonly ITerminalHost _host;
    private readonly ChangeCounter _changes = new();
    private readonly Lock _sync = new();
    private bool[] _tabStops;
    private SavedCursor? _savedPrimary;
    private SavedCursor? _savedAlternate;
    private int _lastPrinted;

    public Terminal(int columns, int rows, ITerminalHost? host = null, int maxScrollback = DefaultScrollback, long maxScrollbackBytes = ScreenBuffer.DefaultScrollbackBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 2);
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1);
        _host = host ?? NullTerminalHost.Instance;
        _parser = new Parser(this);
        Primary = new ScreenBuffer(columns, rows, maxScrollback, _changes, maxScrollbackBytes);
        Alternate = new ScreenBuffer(columns, rows, 0, _changes);
        Screen = Primary;
        ScrollBottom = rows - 1;
        _tabStops = DefaultTabStops(columns);
    }

    public Lock SyncRoot => _sync;

    public ScreenBuffer Primary { get; }

    public ScreenBuffer Alternate { get; }

    public ScreenBuffer Screen { get; private set; }

    public bool IsAlternateScreen => Screen == Alternate;

    public Cursor Cursor { get; } = new();

    public TerminalModes Modes { get; } = new();

    public string Title { get; private set; } = string.Empty;

    public string? WorkingDirectory { get; private set; }

    public string? WorkingDirectoryHost { get; private set; }

    public int Columns => Screen.Columns;

    public int Rows => Screen.Rows;

    public int ScrollTop { get; private set; }

    public int ScrollBottom { get; private set; }

    public long ChangeSequence => _changes.Current;

    public void Feed(ReadOnlySpan<byte> data)
    {
        lock (_sync)
        {
            _parser.Advance(data);
        }
    }

    public void Feed(string text) => Feed(Encoding.UTF8.GetBytes(text));

    // Raised, with the terminal's lock held, when the primary screen's lines move because it reflowed.
    public event EventHandler<LinesMovedEventArgs>? LinesMoved;

    public void Resize(int columns, int rows)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 2);
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1);
        lock (_sync)
        {
            var map = columns == Columns ? null : ReflowPrimary(columns, rows);
            if (map is null)
            {
                ResizeWithoutReflow(columns, rows);
            }
            else
            {
                ResizeAlternate(columns, rows);
            }

            ScrollTop = 0;
            ScrollBottom = rows - 1;
            _tabStops = DefaultTabStops(columns);
            if (map is not null)
            {
                MoveCommands(map);
                LinesMoved?.Invoke(this, new LinesMovedEventArgs(map));
            }
        }
    }

    // Shrinking drops empty rows below the cursor first; rows above it go to scrollback only when the cursor would fall off.
    private void ResizeWithoutReflow(int columns, int rows)
    {
        var dropFromTop = Math.Max(0, Cursor.Row + 1 - rows);
        Primary.Resize(columns, rows, IsAlternateScreen ? 0 : dropFromTop);
        Alternate.Resize(columns, rows, IsAlternateScreen ? dropFromTop : 0);
        Cursor.Row = Math.Clamp(Cursor.Row - dropFromTop, 0, rows - 1);
        ClampCursor(columns, rows);
    }

    // The alternate screen never reflows (full-screen programs redraw for the new size); behind it, the primary screen reflows
    // around the cursor it will return to.
    private LineMap ReflowPrimary(int columns, int rows)
    {
        if (!IsAlternateScreen)
        {
            var map = Primary.Reflow(columns, rows, Cursor);
            if (_savedPrimary is { } saved)
            {
                _savedPrimary = saved with { Row = Math.Min(saved.Row, rows - 1), Column = Math.Min(saved.Column, columns - 1) };
            }

            return map;
        }

        var behind = new Cursor { Row = _savedPrimary?.Row ?? Primary.Rows - 1, Column = _savedPrimary?.Column ?? 0 };
        var moved = Primary.Reflow(columns, rows, behind);
        if (_savedPrimary is { } before)
        {
            _savedPrimary = before with { Row = behind.Row, Column = behind.Column, PendingWrap = behind.PendingWrap };
        }

        return moved;
    }

    private void ResizeAlternate(int columns, int rows)
    {
        var dropFromTop = IsAlternateScreen ? Math.Max(0, Cursor.Row + 1 - rows) : 0;
        Alternate.Resize(columns, rows, dropFromTop);
        if (!IsAlternateScreen)
        {
            return;
        }

        Cursor.Row -= dropFromTop;
        ClampCursor(columns, rows);
    }

    private void ClampCursor(int columns, int rows)
    {
        Cursor.Row = Math.Clamp(Cursor.Row, 0, rows - 1);
        Cursor.Column = Math.Min(Cursor.Column, columns - 1);
        Cursor.PendingWrap = false;
    }

    public string GetLineText(long firstLine, long lastLine)
    {
        lock (_sync)
        {
            var text = new StringBuilder();
            var first = Math.Max(firstLine, Primary.LinesScrolledOff);
            var last = Math.Min(lastLine, Primary.FirstVisibleLine + Rows - 1);
            for (var line = first; line <= last; line++)
            {
                var row = PrimaryLine(line);
                text.Append(row.GetText());
                if (!row.IsWrapped && line < last)
                {
                    text.Append('\n');
                }
            }

            return text.ToString().TrimEnd();
        }
    }

    private Row PrimaryLine(long line)
    {
        var scrollbackIndex = line - Primary.LinesScrolledOff;
        return scrollbackIndex < Primary.Scrollback.Count
            ? Primary.Scrollback[(int)scrollbackIndex]
            : Primary[(int)(line - Primary.FirstVisibleLine)];
    }

    public string GetVisibleText()
    {
        lock (_sync)
        {
            var lines = new string[Rows];
            for (var row = 0; row < Rows; row++)
            {
                lines[row] = Screen[row].GetText();
            }

            return string.Join('\n', lines).TrimEnd('\n');
        }
    }

    public void Print(Rune character) => PrintCodePoint(character.Value);

    public void PrintAscii(ReadOnlySpan<byte> text)
    {
        foreach (var value in text)
        {
            PrintCodePoint(value);
        }
    }

    public void Execute(byte control)
    {
        switch (control)
        {
            case ControlCharacters.Bell:
                _host.Bell();
                break;
            case ControlCharacters.Backspace:
                Backspace();
                break;
            case ControlCharacters.HorizontalTab:
                MoveToNextTabStop(1);
                break;
            case ControlCharacters.LineFeed or ControlCharacters.VerticalTab or ControlCharacters.FormFeed:
                LineFeed();
                break;
            case ControlCharacters.CarriageReturn:
                CarriageReturn();
                break;
            case ControlCharacters.ShiftOut or ControlCharacters.ShiftIn:
                Cursor.UsesG1 = control == ControlCharacters.ShiftOut;
                break;
        }
    }

    private void PrintCodePoint(int codePoint)
    {
        codePoint = Translate(codePoint);
        var width = CharacterWidth.Of(codePoint);
        if (width == 0)
        {
            AttachToPreviousCell(codePoint);
            return;
        }

        WrapIfPending();
        if (width == 2 && Cursor.Column == Columns - 1)
        {
            WrapForWideCharacter();
        }

        WriteCell(codePoint, width);
        _lastPrinted = codePoint;
    }

    private int Translate(int codePoint)
    {
        if (Cursor.ActiveCharset != Charset.DecSpecialGraphics || codePoint is < 0x5F or > 0x7E)
        {
            return codePoint;
        }

        return DecSpecialGraphics.Translate(codePoint);
    }

    private void AttachToPreviousCell(int codePoint)
    {
        var column = Cursor.PendingWrap ? Cursor.Column : Cursor.Column - 1;
        if (column < 0)
        {
            return;
        }

        var row = Screen[Cursor.Row];
        if (row[column].IsWideContinuation && column > 0)
        {
            column--;
        }

        row.AppendToGrapheme(column, char.ConvertFromUtf32(codePoint));
        Screen.Touch(Cursor.Row);
    }

    private void WrapIfPending()
    {
        if (!Cursor.PendingWrap || !Modes.AutoWrap)
        {
            Cursor.PendingWrap = false;
            return;
        }

        Screen[Cursor.Row].IsWrapped = true;
        Cursor.PendingWrap = false;
        Index();
        Cursor.Column = 0;
    }

    private void WrapForWideCharacter()
    {
        if (!Modes.AutoWrap)
        {
            Cursor.Column = Columns - 2;
            return;
        }

        Screen[Cursor.Row].Erase(Cursor.Column, Columns, Cursor.Style);
        Screen[Cursor.Row].IsWrapped = true;
        Index();
        Cursor.Column = 0;
    }

    private void WriteCell(int codePoint, int width)
    {
        var row = Screen[Cursor.Row];
        if (Modes.Insert)
        {
            row.Insert(Cursor.Column, width, Columns, Cursor.Style);
        }

        ClearWideCharacterAt(row, Cursor.Column);
        if (width == 2)
        {
            ClearWideCharacterAt(row, Cursor.Column + 1);
        }

        row.Set(Cursor.Column, new Cell { CodePoint = codePoint, Width = (byte)width, Style = Cursor.Style, HyperlinkId = Cursor.HyperlinkId });
        if (width == 2)
        {
            row.Set(Cursor.Column + 1, new Cell { Width = Cell.WideContinuation, Style = Cursor.Style, HyperlinkId = Cursor.HyperlinkId });
        }

        Screen.Touch(Cursor.Row);
        AdvanceCursor(width);
    }

    private void ClearWideCharacterAt(Row row, int column)
    {
        if (row[column].IsWideContinuation && column > 0)
        {
            row.Set(column - 1, Cell.Blank(Cursor.Style));
            return;
        }

        if (row[column].Width != 2 || column + 1 >= Columns)
        {
            return;
        }

        row.Set(column + 1, Cell.Blank(Cursor.Style));
    }

    private void AdvanceCursor(int width)
    {
        var next = Cursor.Column + width;
        if (next < Columns)
        {
            Cursor.Column = next;
            return;
        }

        Cursor.Column = Columns - 1;
        Cursor.PendingWrap = true;
    }

    private void Backspace()
    {
        Cursor.PendingWrap = false;
        Cursor.Column = Math.Max(0, Cursor.Column - 1);
    }

    private void CarriageReturn()
    {
        Cursor.PendingWrap = false;
        Cursor.Column = 0;
    }

    private void LineFeed()
    {
        Index();
        if (!Modes.LineFeedNewLine)
        {
            return;
        }

        CarriageReturn();
    }

    private void Index()
    {
        Cursor.PendingWrap = false;
        if (Cursor.Row == ScrollBottom)
        {
            Screen.ScrollUp(ScrollTop, ScrollBottom, 1, Cursor.Style);
            return;
        }

        Cursor.Row = Math.Min(Cursor.Row + 1, Rows - 1);
    }

    private void ReverseIndex()
    {
        Cursor.PendingWrap = false;
        if (Cursor.Row == ScrollTop)
        {
            Screen.ScrollDown(ScrollTop, ScrollBottom, 1, Cursor.Style);
            return;
        }

        Cursor.Row = Math.Max(Cursor.Row - 1, 0);
    }

    private void MoveToNextTabStop(int count)
    {
        for (var step = 0; step < count; step++)
        {
            var column = Cursor.Column + 1;
            while (column < Columns - 1 && !_tabStops[column])
            {
                column++;
            }

            Cursor.Column = Math.Min(column, Columns - 1);
        }
    }

    private void MoveToPreviousTabStop(int count)
    {
        for (var step = 0; step < count; step++)
        {
            var column = Cursor.Column - 1;
            while (column > 0 && !_tabStops[column])
            {
                column--;
            }

            Cursor.Column = Math.Max(column, 0);
        }
    }

    private void MoveCursorTo(int row, int column)
    {
        var top = Modes.Origin ? ScrollTop : 0;
        var bottom = Modes.Origin ? ScrollBottom : Rows - 1;
        Cursor.Row = Math.Clamp(top + row, top, bottom);
        Cursor.Column = Math.Clamp(column, 0, Columns - 1);
        Cursor.PendingWrap = false;
    }

    private void MoveCursorBy(int rows, int columns)
    {
        var top = Cursor.Row >= ScrollTop ? ScrollTop : 0;
        var bottom = Cursor.Row <= ScrollBottom ? ScrollBottom : Rows - 1;
        Cursor.Row = Math.Clamp(Cursor.Row + rows, top, bottom);
        Cursor.Column = Math.Clamp(Cursor.Column + columns, 0, Columns - 1);
        Cursor.PendingWrap = false;
    }

    private static bool[] DefaultTabStops(int columns)
    {
        var stops = new bool[columns];
        for (var column = 8; column < columns; column += 8)
        {
            stops[column] = true;
        }

        return stops;
    }
}
