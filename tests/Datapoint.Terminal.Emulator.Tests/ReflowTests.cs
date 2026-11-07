using Datapoint.Terminal.Emulator.Screen;

namespace Datapoint.Terminal.Emulator.Tests;

public sealed class ReflowTests
{
    [Fact]
    public void Rewraps_a_long_line_when_the_window_narrows()
    {
        var terminal = TerminalBuilder.With("abcdefghij", columns: 20, rows: 4);

        terminal.Resize(4, 4);

        Assert.Equal("abcd\nefgh\nij", RowsOf(terminal));
        Assert.Equal((2, 2), (terminal.Cursor.Row, terminal.Cursor.Column));
    }

    [Fact]
    public void Joins_wrapped_rows_when_the_window_widens()
    {
        var terminal = TerminalBuilder.With("abcdefghij\r\nnext", columns: 4, rows: 6);

        terminal.Resize(20, 6);

        Assert.Equal("abcdefghij\nnext", RowsOf(terminal));
        Assert.Equal((1, 4), (terminal.Cursor.Row, terminal.Cursor.Column));
        Assert.Equal("abcdefghij\nnext", terminal.GetLineText(0, 1));
    }

    [Fact]
    public void Keeps_the_text_the_same_through_a_round_trip()
    {
        var output = string.Join("\r\n", Enumerable.Range(1, 30).Select(index => $"line {index} " + new string('x', index)));
        var terminal = TerminalBuilder.With(output, columns: 40, rows: 10);
        var before = terminal.GetLineText(0, long.MaxValue);

        terminal.Resize(13, 10);
        terminal.Resize(40, 10);

        Assert.Equal(before, terminal.GetLineText(0, long.MaxValue));
        Assert.Equal(9, terminal.Cursor.Row);
    }

    [Fact]
    public void Never_splits_a_wide_character()
    {
        var terminal = TerminalBuilder.With("ab日本", columns: 10, rows: 4);

        terminal.Resize(5, 4);

        Assert.Equal("ab日\n本", RowsOf(terminal));
    }

    [Fact]
    public void Moves_command_marks_with_their_lines()
    {
        var terminal = TerminalBuilder.With("abcdefghijklmnop\r\n\e]133;A\a$ \e]133;B\a", columns: 20, rows: 6);
        var prompt = terminal.CurrentCommand!.PromptLine;
        long? moved = null;
        terminal.LinesMoved += (_, e) => moved = e.Map.Map(prompt);

        terminal.Resize(4, 6);

        Assert.Equal(4, terminal.CurrentCommand!.PromptLine);
        Assert.Equal(4, moved);
        Assert.Equal("$", terminal.GetLineText(4, 4));
    }

    [Fact]
    public void Leaves_the_alternate_screen_alone_and_reflows_the_primary_behind_it()
    {
        var terminal = TerminalBuilder.With("abcdefghij\e[?1049h\e[Hfull screen", columns: 20, rows: 4);

        terminal.Resize(5, 4);

        Assert.StartsWith("full", terminal.GetVisibleText(), StringComparison.Ordinal);
        terminal.Feed("\e[?1049l");
        Assert.Equal("abcde\nfghij", RowsOf(terminal));
    }

    [Fact]
    public void Limits_the_scrollback_by_its_size_in_memory()
    {
        var terminal = new Terminal(80, 4, maxScrollback: 100_000, maxScrollbackBytes: 64 * 1024);

        terminal.Feed(string.Concat(Enumerable.Range(1, 5_000).Select(index => $"line {index} {new string('y', 60)}\r\n")));

        Assert.InRange(terminal.Primary.ScrollbackBytes, 1, 64 * 1024);
        Assert.InRange(terminal.Primary.Scrollback.Count, 10, 4_999);
        Assert.Equal(5_000 - terminal.Primary.Scrollback.Count - 3, terminal.Primary.LinesScrolledOff);
    }

    [Fact]
    public void Keeps_only_the_content_of_rows_that_scroll_away()
    {
        var terminal = TerminalBuilder.With("short\r\n\r\n\r\n\r\n", columns: 200, rows: 4);

        var row = Assert.Single(terminal.Primary.Scrollback);

        Assert.Equal(5, row.Columns);
        Assert.Equal("short", row.GetText());
    }

    private static string RowsOf(Terminal terminal) =>
        string.Join("\n", Enumerable.Range(0, terminal.Rows).Select(row => terminal.Screen[row].GetText())).TrimEnd('\n');
}
