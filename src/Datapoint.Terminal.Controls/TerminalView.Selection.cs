using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Datapoint.Terminal.Emulator.Screen;
using EmulatorTerminal = Datapoint.Terminal.Emulator.Terminal;

namespace Datapoint.Terminal.Controls;

// Selecting and copying text, and finding it in the scrollback. Neither sends anything to the terminal, so neither interrupts
// the agent (product decision 14).
public sealed partial class TerminalView
{
    public static readonly StyledProperty<IBrush?> SelectionBrushProperty =
        AvaloniaProperty.Register<TerminalView, IBrush?>(nameof(SelectionBrush), new SolidColorBrush(Color.FromArgb(0x66, 0x3A, 0x8D, 0xFF)));

    private TextPosition? _anchor;
    private TextPosition? _caret;
    private bool _isSelecting;
    private bool _selectionOnAlternate;

    public IBrush? SelectionBrush
    {
        get => GetValue(SelectionBrushProperty);
        set => SetValue(SelectionBrushProperty, value);
    }

    public bool HasSelection => _anchor is { } anchor && _caret is { } caret && anchor != caret;

    public string SelectedText
    {
        get
        {
            var terminal = _terminal;
            if (terminal is null || !HasSelection)
            {
                return string.Empty;
            }

            lock (terminal.SyncRoot)
            {
                return TextBetween(terminal, Start, End);
            }
        }
    }

    private TextPosition Start => _anchor!.Value < _caret!.Value ? _anchor.Value : _caret.Value;

    private TextPosition End => _anchor!.Value < _caret!.Value ? _caret.Value : _anchor.Value;

    public void ClearSelection()
    {
        _anchor = _caret = null;
        InvalidateVisual();
    }

    // Selects a match and scrolls so it's in view; used by search.
    public void Select(TextPosition start, TextPosition end)
    {
        var terminal = _terminal;
        if (terminal is null)
        {
            return;
        }

        _selectionOnAlternate = terminal.IsAlternateScreen;
        (_anchor, _caret) = (start, end);
        lock (terminal.SyncRoot)
        {
            ScrollIntoView(terminal, start.Line);
        }

        InvalidateVisual();
    }

    public (TextPosition Start, TextPosition End)? Find(string query, bool older) =>
        string.IsNullOrEmpty(query) ? null : Find(TextSearch.ForText(query), older);

    // The next match before (older) or after the given position, wrapping around.
    public (TextPosition Start, TextPosition End)? Find(TextSearch query, bool older)
    {
        var terminal = _terminal;
        if (terminal is null || query is null)
        {
            return null;
        }

        lock (terminal.SyncRoot)
        {
            var (first, last) = LineRange(terminal);
            var from = HasSelection ? Start : new TextPosition(older ? last + 1 : first - 1, 0);
            var count = last - first + 1;
            for (var step = 0; step <= count; step++)
            {
                var line = older ? from.Line - step : from.Line + step;
                line = first + ((((line - first) % count) + count) % count);
                if (MatchIn(terminal, line, query, older, step == 0 ? from : null) is { } match)
                {
                    return match;
                }
            }
        }

        return null;
    }

    public int CountMatches(string query) => string.IsNullOrEmpty(query) ? 0 : CountMatches(TextSearch.ForText(query));

    public int CountMatches(TextSearch query)
    {
        var terminal = _terminal;
        if (terminal is null || query is null)
        {
            return 0;
        }

        lock (terminal.SyncRoot)
        {
            var (first, last) = LineRange(terminal);
            var total = 0;
            for (var line = first; line <= last; line++)
            {
                total += query.In(LineText(terminal, line).Text).Count();
            }

            return total;
        }
    }

    private static (TextPosition, TextPosition)? MatchIn(EmulatorTerminal terminal, long line, TextSearch query, bool older, TextPosition? current)
    {
        var (text, columns) = LineText(terminal, line);
        var matches = query.In(text).ToList();
        var indexes = matches.Select(match => match.Index).ToList();

        // On the current match's line, only matches beyond it count.
        var candidates = current is { } position
            ? indexes.Where(index => older ? columns[index] < position.Column : columns[index] > position.Column)
            : indexes;
        var found = older ? candidates.Cast<int?>().LastOrDefault() : candidates.Cast<int?>().FirstOrDefault();
        if (found is not { } start)
        {
            return null;
        }

        var endIndex = start + matches.First(match => match.Index == start).Length;
        var endColumn = endIndex < columns.Count ? columns[endIndex] : columns[^1] + 1;
        return (new TextPosition(line, columns[start]), new TextPosition(line, endColumn));
    }

    // The primary screen's kept lines, or the alternate screen's rows.
    private static (long First, long Last) LineRange(EmulatorTerminal terminal) => terminal.IsAlternateScreen
        ? (0, terminal.Rows - 1)
        : (terminal.Primary.LinesScrolledOff, terminal.Primary.FirstVisibleLine + terminal.Rows - 1);

    private static Row? RowAtLine(EmulatorTerminal terminal, long line)
    {
        if (terminal.IsAlternateScreen)
        {
            return line >= 0 && line < terminal.Rows ? terminal.Screen[(int)line] : null;
        }

        var index = line - terminal.Primary.LinesScrolledOff;
        var scrollback = terminal.Primary.Scrollback;
        return index < 0 ? null
            : index < scrollback.Count ? scrollback[(int)index]
            : index - scrollback.Count < terminal.Rows ? terminal.Primary[(int)(index - scrollback.Count)]
            : null;
    }

    // A line's text, and the column each character came from; the last entry is one past the end.
    private static (string Text, List<int> Columns) LineText(EmulatorTerminal terminal, long line)
    {
        var text = new StringBuilder();
        var columns = new List<int>();
        if (RowAtLine(terminal, line) is not { } row)
        {
            columns.Add(0);
            return (string.Empty, columns);
        }

        for (var column = 0; column < row.Columns; column++)
        {
            var cell = row[column];
            if (cell.IsWideContinuation)
            {
                continue;
            }

            var characters = cell.CodePoint == 0 ? " " : cell.Text;
            text.Append(characters);
            columns.AddRange(Enumerable.Repeat(column, characters.Length));
        }

        columns.Add(row.Columns);
        return (text.ToString(), columns);
    }

    private static string TextBetween(EmulatorTerminal terminal, TextPosition start, TextPosition end)
    {
        var text = new StringBuilder();
        for (var line = start.Line; line <= end.Line; line++)
        {
            var (lineText, columns) = LineText(terminal, line);
            var from = line == start.Line ? columns.FindIndex(column => column >= start.Column) : 0;
            var to = line == end.Line ? columns.FindIndex(column => column >= end.Column) : lineText.Length;
            from = from < 0 ? lineText.Length : from;
            to = to < 0 ? lineText.Length : to;
            text.Append(lineText[from..Math.Max(from, to)].TrimEnd(' '));
            if (line < end.Line && RowAtLine(terminal, line) is not { IsWrapped: true })
            {
                text.Append('\n');
            }
        }

        return text.ToString();
    }

    private TextPosition PositionAt(EmulatorTerminal terminal, Point point)
    {
        var metrics = _renderer.Metrics;
        var row = Math.Clamp((int)Math.Floor((point.Y - Padding.Top) / metrics.Height), 0, terminal.Rows - 1);
        var column = Math.Clamp((int)Math.Round((point.X - Padding.Left) / metrics.Width), 0, terminal.Columns);
        return new TextPosition(TopLine(terminal) + row, column);
    }

    private long TopLine(EmulatorTerminal terminal) => terminal.IsAlternateScreen ? 0 : terminal.Primary.FirstVisibleLine - _scrollOffset;

    private void ScrollIntoView(EmulatorTerminal terminal, long line)
    {
        if (terminal.IsAlternateScreen)
        {
            return;
        }

        var top = TopLine(terminal);
        if (line >= top && line < top + terminal.Rows)
        {
            return;
        }

        // Puts the line a third of the way down, so what led to it shows too.
        var wantedTop = line - (terminal.Rows / 3);
        _scrollOffset = (int)Math.Clamp(terminal.Primary.FirstVisibleLine - wantedTop, 0, terminal.Primary.Scrollback.Count);
    }

    private void DrawSelection(DrawingContext context, EmulatorTerminal terminal)
    {
        if (!HasSelection || _selectionOnAlternate != terminal.IsAlternateScreen)
        {
            return;
        }

        var metrics = _renderer.Metrics;
        var top = TopLine(terminal);
        var (start, end) = (Start, End);
        for (var row = 0; row < terminal.Rows; row++)
        {
            var line = top + row;
            if (line < start.Line || line > end.Line)
            {
                continue;
            }

            var from = line == start.Line ? start.Column : 0;
            var to = line == end.Line ? end.Column : terminal.Columns;
            context.DrawRectangle(SelectionBrush, null, new Rect(from * metrics.Width, row * metrics.Height, Math.Max(0, to - from) * metrics.Width, metrics.Height));
        }
    }

    private bool StartSelection(PointerPressedEventArgs e)
    {
        var terminal = _terminal;
        var point = e.GetCurrentPoint(this);
        if (terminal is null || !point.Properties.IsLeftButtonPressed)
        {
            return false;
        }

        lock (terminal.SyncRoot)
        {
            var position = PositionAt(terminal, point.Position);
            _selectionOnAlternate = terminal.IsAlternateScreen;
            if (e.ClickCount >= 2)
            {
                SelectWordOrLine(terminal, position, e.ClickCount);
                return true;
            }

            // Shift-click extends the selection to where was clicked, as in text editors.
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && _anchor is not null)
            {
                _caret = position;
            }
            else
            {
                (_anchor, _caret) = (position, position);
            }
        }

        _isSelecting = true;
        e.Pointer.Capture(this);
        InvalidateVisual();
        return true;
    }

    private void SelectWordOrLine(EmulatorTerminal terminal, TextPosition position, int clicks)
    {
        if (clicks == 2)
        {
            SelectWord(terminal, position);
            return;
        }

        // A line that wrapped is selected whole, from the row it starts on to the row it ends on.
        var (first, last) = LineRange(terminal);
        var start = position.Line;
        while (start > first && RowAtLine(terminal, start - 1) is { IsWrapped: true })
        {
            start--;
        }

        var end = position.Line;
        while (end < last && RowAtLine(terminal, end) is { IsWrapped: true })
        {
            end++;
        }

        var (text, columns) = LineText(terminal, end);
        (_anchor, _caret) = (new TextPosition(start, 0), new TextPosition(end, columns[text.TrimEnd(' ').Length]));
        InvalidateVisual();
    }

    private void SelectWord(EmulatorTerminal terminal, TextPosition position)
    {
        var (text, columns) = LineText(terminal, position.Line);
        var index = columns.FindIndex(column => column >= position.Column);
        if (index < 0 || index >= text.Length || char.IsWhiteSpace(text[index]))
        {
            return;
        }

        static bool IsWord(char character) => !char.IsWhiteSpace(character) && "\"'`()[]{}<>|;,".IndexOf(character, StringComparison.Ordinal) < 0;
        var start = index;
        while (start > 0 && IsWord(text[start - 1]))
        {
            start--;
        }

        var end = index;
        while (end < text.Length && IsWord(text[end]))
        {
            end++;
        }

        (_anchor, _caret) = (new TextPosition(position.Line, columns[start]), new TextPosition(position.Line, columns[end]));
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPointerMoved(e);
        var terminal = _terminal;
        if (!_isSelecting || terminal is null)
        {
            return;
        }

        lock (terminal.SyncRoot)
        {
            _caret = PositionAt(terminal, e.GetPosition(this));
        }

        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPointerReleased(e);
        if (!_isSelecting)
        {
            return;
        }

        _isSelecting = false;
        e.Pointer.Capture(null);
    }

    private static bool IsCopyGesture(KeyEventArgs e) => OperatingSystem.IsMacOS()
        ? e.Key == Key.C && e.KeyModifiers == KeyModifiers.Meta
        : e.Key == Key.C && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift);

    private async Task CopyAsync()
    {
        var text = SelectedText;
        if (text.Length == 0 || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        await clipboard.SetTextAsync(text).ConfigureAwait(true);
    }
}
