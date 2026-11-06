using System.Collections.Frozen;
using Datapoint.Terminal.Emulator.Parsing;
using Datapoint.Terminal.Emulator.Screen;

namespace Datapoint.Terminal.Emulator;

public sealed partial class Terminal
{
    private delegate void CsiHandler(Terminal terminal, CsiParameters parameters);

    private static readonly FrozenDictionary<int, CsiHandler> s_csiHandlers = new Dictionary<int, CsiHandler>
    {
        [CsiKey('@')] = (terminal, parameters) => terminal.InsertCharacters(parameters.GetOrDefault(0, 1)),
        [CsiKey('A')] = (terminal, parameters) => terminal.MoveCursorBy(-parameters.GetOrDefault(0, 1), 0),
        [CsiKey('B')] = (terminal, parameters) => terminal.MoveCursorBy(parameters.GetOrDefault(0, 1), 0),
        [CsiKey('C')] = (terminal, parameters) => terminal.MoveCursorBy(0, parameters.GetOrDefault(0, 1)),
        [CsiKey('D')] = (terminal, parameters) => terminal.MoveCursorBy(0, -parameters.GetOrDefault(0, 1)),
        [CsiKey('E')] = (terminal, parameters) => terminal.MoveToLineStart(parameters.GetOrDefault(0, 1)),
        [CsiKey('F')] = (terminal, parameters) => terminal.MoveToLineStart(-parameters.GetOrDefault(0, 1)),
        [CsiKey('G')] = (terminal, parameters) => terminal.MoveToColumn(parameters.GetOrDefault(0, 1) - 1),
        [CsiKey('H')] = (terminal, parameters) => terminal.MoveCursorTo(parameters.GetOrDefault(0, 1) - 1, parameters.GetOrDefault(1, 1) - 1),
        [CsiKey('I')] = (terminal, parameters) => terminal.MoveToNextTabStop(parameters.GetOrDefault(0, 1)),
        [CsiKey('J')] = (terminal, parameters) => terminal.EraseInDisplay(parameters.GetOrDefault(0, 0)),
        [CsiKey('K')] = (terminal, parameters) => terminal.EraseInLine(parameters.GetOrDefault(0, 0)),
        [CsiKey('L')] = (terminal, parameters) => terminal.InsertLines(parameters.GetOrDefault(0, 1)),
        [CsiKey('M')] = (terminal, parameters) => terminal.DeleteLines(parameters.GetOrDefault(0, 1)),
        [CsiKey('P')] = (terminal, parameters) => terminal.DeleteCharacters(parameters.GetOrDefault(0, 1)),
        [CsiKey('S')] = (terminal, parameters) => terminal.Screen.ScrollUp(terminal.ScrollTop, terminal.ScrollBottom, parameters.GetOrDefault(0, 1), terminal.Cursor.Style),
        [CsiKey('T')] = (terminal, parameters) => terminal.Screen.ScrollDown(terminal.ScrollTop, terminal.ScrollBottom, parameters.GetOrDefault(0, 1), terminal.Cursor.Style),
        [CsiKey('X')] = (terminal, parameters) => terminal.EraseCharacters(parameters.GetOrDefault(0, 1)),
        [CsiKey('Z')] = (terminal, parameters) => terminal.MoveToPreviousTabStop(parameters.GetOrDefault(0, 1)),
        [CsiKey('`')] = (terminal, parameters) => terminal.MoveToColumn(parameters.GetOrDefault(0, 1) - 1),
        [CsiKey('a')] = (terminal, parameters) => terminal.MoveCursorBy(0, parameters.GetOrDefault(0, 1)),
        [CsiKey('b')] = (terminal, parameters) => terminal.RepeatLastCharacter(parameters.GetOrDefault(0, 1)),
        [CsiKey('c')] = (terminal, parameters) => terminal.ReplyPrimaryDeviceAttributes(parameters.GetOrDefault(0, 0)),
        [CsiKey('c', '>')] = (terminal, parameters) => terminal.ReplySecondaryDeviceAttributes(parameters.GetOrDefault(0, 0)),
        [CsiKey('d')] = (terminal, parameters) => terminal.MoveToRow(parameters.GetOrDefault(0, 1) - 1),
        [CsiKey('e')] = (terminal, parameters) => terminal.MoveCursorBy(parameters.GetOrDefault(0, 1), 0),
        [CsiKey('f')] = (terminal, parameters) => terminal.MoveCursorTo(parameters.GetOrDefault(0, 1) - 1, parameters.GetOrDefault(1, 1) - 1),
        [CsiKey('g')] = (terminal, parameters) => terminal.ClearTabStops(parameters.GetOrDefault(0, 0)),
        [CsiKey('h')] = (terminal, parameters) => terminal.SetAnsiModes(parameters, enabled: true),
        [CsiKey('l')] = (terminal, parameters) => terminal.SetAnsiModes(parameters, enabled: false),
        [CsiKey('h', '?')] = (terminal, parameters) => terminal.SetPrivateModes(parameters, enabled: true),
        [CsiKey('l', '?')] = (terminal, parameters) => terminal.SetPrivateModes(parameters, enabled: false),
        [CsiKey('m')] = (terminal, parameters) => terminal.SelectGraphicRendition(parameters),
        [CsiKey('n')] = (terminal, parameters) => terminal.ReplyDeviceStatus(parameters.GetOrDefault(0, 0)),
        [CsiKey('q', '>')] = (terminal, _) => terminal.ReplyVersion(),
        [CsiKey('r')] = (terminal, parameters) => terminal.SetScrollRegion(parameters.GetOrDefault(0, 1) - 1, parameters.GetOrDefault(1, terminal.Rows) - 1),
        [CsiKey('s')] = (terminal, _) => terminal.SaveCursor(),
        [CsiKey('u')] = (terminal, _) => terminal.RestoreCursor(),
        [CsiKey('p', '?', '$')] = (terminal, parameters) => terminal.ReplyPrivateModeState(parameters.GetOrDefault(0, 0)),
        [CsiKey('p', '\0', '$')] = (terminal, parameters) => terminal.ReplyAnsiModeState(parameters.GetOrDefault(0, 0)),
        [CsiKey('p', '\0', '!')] = (terminal, _) => terminal.SoftReset(),
        [CsiKey('q', '\0', ' ')] = (terminal, parameters) => terminal.SetCursorShape(parameters.GetOrDefault(0, 0)),
    }.ToFrozenDictionary();

    public void CsiDispatch(CsiSequence sequence)
    {
        var intermediate = sequence.Intermediates.Length == 1 ? sequence.Intermediates[0] : (byte)0;
        if (sequence.Intermediates.Length > 1)
        {
            return;
        }

        var key = CsiKey((char)sequence.Final, (char)sequence.PrivateMarker, (char)intermediate);
        if (!s_csiHandlers.TryGetValue(key, out var handler))
        {
            return;
        }

        handler(this, sequence.Parameters);
    }

    private static int CsiKey(char final, char privateMarker = '\0', char intermediate = '\0') =>
        (privateMarker << 16) | (intermediate << 8) | final;

    private void MoveToLineStart(int rows)
    {
        MoveCursorBy(rows, 0);
        Cursor.Column = 0;
    }

    private void MoveToColumn(int column)
    {
        Cursor.Column = Math.Clamp(column, 0, Columns - 1);
        Cursor.PendingWrap = false;
    }

    private void MoveToRow(int row) => MoveCursorTo(row, Cursor.Column);

    private void InsertCharacters(int count)
    {
        Cursor.PendingWrap = false;
        Screen[Cursor.Row].Insert(Cursor.Column, count, Columns, Cursor.Style);
        Screen.Touch(Cursor.Row);
    }

    private void DeleteCharacters(int count)
    {
        Cursor.PendingWrap = false;
        Screen[Cursor.Row].Delete(Cursor.Column, count, Columns, Cursor.Style);
        Screen.Touch(Cursor.Row);
    }

    private void EraseCharacters(int count)
    {
        Cursor.PendingWrap = false;
        var end = Math.Min(Columns, Cursor.Column + count);
        Screen[Cursor.Row].Erase(Cursor.Column, end, Cursor.Style);
        Screen.Touch(Cursor.Row);
    }

    private void InsertLines(int count)
    {
        if (Cursor.Row < ScrollTop || Cursor.Row > ScrollBottom)
        {
            return;
        }

        Screen.ScrollDown(Cursor.Row, ScrollBottom, count, Cursor.Style);
        Cursor.Column = 0;
        Cursor.PendingWrap = false;
    }

    private void DeleteLines(int count)
    {
        if (Cursor.Row < ScrollTop || Cursor.Row > ScrollBottom)
        {
            return;
        }

        Screen.ScrollUpWithoutScrollback(Cursor.Row, ScrollBottom, count, Cursor.Style);
        Cursor.Column = 0;
        Cursor.PendingWrap = false;
    }

    private void EraseInDisplay(int mode)
    {
        switch (mode)
        {
            case 0:
                EraseInLine(0);
                EraseRowsSafely(Cursor.Row + 1, Rows - 1);
                break;
            case 1:
                EraseRowsSafely(0, Cursor.Row - 1);
                EraseInLine(1);
                break;
            case 2:
                Screen.EraseRows(0, Rows - 1, Cursor.Style);
                break;
            case 3:
                Screen.ClearScrollback();
                break;
        }
    }

    private void EraseRowsSafely(int top, int bottom)
    {
        if (top > bottom)
        {
            return;
        }

        Screen.EraseRows(top, bottom, Cursor.Style);
    }

    private void EraseInLine(int mode)
    {
        var row = Screen[Cursor.Row];
        var (start, end) = mode switch
        {
            0 => (Cursor.Column, Columns),
            1 => (0, Cursor.Column + 1),
            _ => (0, Columns),
        };
        row.Erase(start, end, Cursor.Style);
        if (mode != 1)
        {
            row.IsWrapped = false;
        }

        Screen.Touch(Cursor.Row);
    }

    private void RepeatLastCharacter(int count)
    {
        if (_lastPrinted == 0)
        {
            return;
        }

        var repeat = Math.Min(count, Columns * Rows);
        for (var step = 0; step < repeat; step++)
        {
            PrintCodePoint(_lastPrinted);
        }
    }

    private void ClearTabStops(int mode)
    {
        if (mode == 0)
        {
            _tabStops[Cursor.Column] = false;
            return;
        }

        if (mode != 3)
        {
            return;
        }

        Array.Clear(_tabStops);
    }

    private void SetScrollRegion(int top, int bottom)
    {
        bottom = Math.Min(bottom, Rows - 1);
        if (top >= bottom)
        {
            return;
        }

        ScrollTop = top;
        ScrollBottom = bottom;
        MoveCursorTo(0, 0);
    }

    private void SaveCursor()
    {
        var saved = Cursor.Save(Modes.Origin);
        if (IsAlternateScreen)
        {
            _savedAlternate = saved;
            return;
        }

        _savedPrimary = saved;
    }

    private void RestoreCursor()
    {
        var saved = IsAlternateScreen ? _savedAlternate : _savedPrimary;
        if (saved is null)
        {
            Cursor.Restore(Cursor.Save(originMode: false) with { Row = 0, Column = 0, Style = CellStyle.Default, PendingWrap = false });
            Modes.Origin = false;
            return;
        }

        Cursor.Restore(saved);
        Modes.Origin = saved.OriginMode;
        Cursor.Row = Math.Min(Cursor.Row, Rows - 1);
        Cursor.Column = Math.Min(Cursor.Column, Columns - 1);
    }

    private void SoftReset()
    {
        Modes.Insert = false;
        Modes.Origin = false;
        Modes.AutoWrap = true;
        Modes.CursorVisible = true;
        Modes.ApplicationCursorKeys = false;
        Modes.ApplicationKeypad = false;
        ScrollTop = 0;
        ScrollBottom = Rows - 1;
        Cursor.Style = CellStyle.Default;
        Cursor.G0 = Charset.Ascii;
        Cursor.G1 = Charset.Ascii;
        Cursor.UsesG1 = false;
        _savedPrimary = null;
        _savedAlternate = null;
    }

    private void SetCursorShape(int shape)
    {
        Modes.CursorShape = shape is >= 0 and <= 6 ? (CursorShape)shape : CursorShape.Default;
    }
}
