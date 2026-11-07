using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using EmulatorTerminal = Datapoint.Terminal.Emulator.Terminal;

namespace Datapoint.Terminal.Controls;

// Selecting without a mouse (keyboard-only operation, CLAUDE.md). While it's on, keys move a selection caret instead of going to
// the shell: arrows, Home, End, Page Up and Page Down move it, with Shift they extend the selection, Enter or the copy shortcut
// copies and ends it, and Escape ends it. Nothing reaches the terminal meanwhile, so the agent isn't interrupted either.
public sealed partial class TerminalView
{
    public event EventHandler? KeyboardSelectionChanged;

    public bool IsSelectingWithKeyboard { get; private set; }

    // Starts where the terminal's cursor is.
    public void StartKeyboardSelection()
    {
        var terminal = _terminal;
        if (terminal is null)
        {
            return;
        }

        lock (terminal.SyncRoot)
        {
            var line = terminal.IsAlternateScreen ? terminal.Cursor.Row : terminal.Primary.FirstVisibleLine + terminal.Cursor.Row;
            var position = new TextPosition(line, terminal.Cursor.Column);
            (_anchor, _caret) = (position, position);
            _selectionOnAlternate = terminal.IsAlternateScreen;
        }

        IsSelectingWithKeyboard = true;
        KeyboardSelectionChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    public void StopKeyboardSelection()
    {
        if (!IsSelectingWithKeyboard)
        {
            return;
        }

        IsSelectingWithKeyboard = false;
        KeyboardSelectionChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    private void HandleSelectionKey(EmulatorTerminal terminal, KeyEventArgs e)
    {
        e.Handled = true;
        if (e.Key == Key.Escape)
        {
            ClearSelection();
            StopKeyboardSelection();
            return;
        }

        if (e.Key == Key.Enter || IsCopyGesture(e))
        {
            _ = CopyAsync();
            StopKeyboardSelection();
            return;
        }

        lock (terminal.SyncRoot)
        {
            if (_caret is not { } caret || Moved(terminal, caret, e.Key) is not { } moved)
            {
                return;
            }

            _caret = moved;
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                _anchor = moved;
            }

            ScrollIntoView(terminal, moved.Line);
        }

        InvalidateVisual();
    }

    private static TextPosition? Moved(EmulatorTerminal terminal, TextPosition caret, Key key)
    {
        var (first, last) = LineRange(terminal);
        var line = key switch
        {
            Key.Up => caret.Line - 1,
            Key.Down => caret.Line + 1,
            Key.PageUp => caret.Line - terminal.Rows,
            Key.PageDown => caret.Line + terminal.Rows,
            _ => caret.Line,
        };
        var column = key switch
        {
            Key.Left => caret.Column - 1,
            Key.Right => caret.Column + 1,
            Key.Home => 0,
            Key.End => LineEnd(terminal, caret.Line),
            Key.Up or Key.Down or Key.PageUp or Key.PageDown => caret.Column,
            _ => -1,
        };
        return column < 0 && key != Key.Left
            ? null
            : new TextPosition(Math.Clamp(line, first, last), Math.Clamp(column, 0, terminal.Columns));
    }

    private static int LineEnd(EmulatorTerminal terminal, long line)
    {
        var (text, columns) = LineText(terminal, line);
        return columns[text.TrimEnd(' ').Length];
    }

    // An outlined block where the selection caret is, so it's clear keys move it and not the shell's cursor.
    private void DrawSelectionCaret(DrawingContext context, EmulatorTerminal terminal)
    {
        if (!IsSelectingWithKeyboard || _caret is not { } caret)
        {
            return;
        }

        var row = caret.Line - TopLine(terminal);
        if (row < 0 || row >= terminal.Rows)
        {
            return;
        }

        var metrics = _renderer.Metrics;
        var rect = new Rect(caret.Column * metrics.Width, row * metrics.Height, 2, metrics.Height);
        context.DrawRectangle(SelectionCaretBrush, null, rect);
    }

    private IBrush SelectionCaretBrush => SelectionBrush is ISolidColorBrush solid ? new SolidColorBrush(solid.Color, 1) : Brushes.DodgerBlue;
}
