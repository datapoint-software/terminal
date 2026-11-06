using Datapoint.Terminal.Emulator.Screen;

namespace Datapoint.Terminal.Emulator;

public sealed partial class Terminal
{
    public void EscDispatch(ReadOnlySpan<byte> intermediates, byte final)
    {
        if (intermediates.IsEmpty)
        {
            EscWithoutIntermediate(final);
            return;
        }

        if (intermediates.Length != 1)
        {
            return;
        }

        EscWithIntermediate(intermediates[0], final);
    }

    private void EscWithoutIntermediate(byte final)
    {
        switch ((char)final)
        {
            case '7':
                SaveCursor();
                break;
            case '8':
                RestoreCursor();
                break;
            case 'D':
                Index();
                break;
            case 'E':
                Index();
                CarriageReturn();
                break;
            case 'M':
                ReverseIndex();
                break;
            case 'H':
                _tabStops[Cursor.Column] = true;
                break;
            case 'c':
                FullReset();
                break;
            default:
                SetKeypadMode(final);
                break;
        }
    }

    private void SetKeypadMode(byte final)
    {
        if (final is not ((byte)'=' or (byte)'>'))
        {
            return;
        }

        Modes.ApplicationKeypad = final == (byte)'=';
    }

    private void EscWithIntermediate(byte intermediate, byte final)
    {
        var charset = final == (byte)'0' ? Charset.DecSpecialGraphics : Charset.Ascii;
        switch ((char)intermediate)
        {
            case '(':
                Cursor.G0 = charset;
                break;
            case ')':
                Cursor.G1 = charset;
                break;
            case '#' when final == (byte)'8':
                FillWithTestPattern();
                break;
        }
    }

    private void FillWithTestPattern()
    {
        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                Screen[row].Set(column, new Cell { CodePoint = 'E', Width = 1, Style = CellStyle.Default });
            }

            Screen.Touch(row);
        }

        ScrollTop = 0;
        ScrollBottom = Rows - 1;
        MoveCursorTo(0, 0);
    }

    private void FullReset()
    {
        Modes.Reset();
        SoftReset();
        Screen = Primary;
        Primary.ClearScrollback();
        Primary.EraseRows(0, Rows - 1, CellStyle.Default);
        Alternate.EraseRows(0, Rows - 1, CellStyle.Default);
        _tabStops = DefaultTabStops(Columns);
        Cursor.Row = 0;
        Cursor.Column = 0;
        Cursor.PendingWrap = false;
        Cursor.HyperlinkId = 0;
        Title = string.Empty;
    }
}
