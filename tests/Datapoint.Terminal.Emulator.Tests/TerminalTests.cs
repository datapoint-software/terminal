using Datapoint.Terminal.Emulator.Screen;
using Datapoint.Terminal.Emulator.ShellIntegration;

namespace Datapoint.Terminal.Emulator.Tests;

public sealed class TerminalTests
{
    [Fact]
    public void Prints_text_at_the_cursor()
    {
        var terminal = TerminalBuilder.With("hello");

        Assert.Equal("hello", terminal.GetVisibleText());
        Assert.Equal(5, terminal.Cursor.Column);
    }

    [Fact]
    public void Wraps_text_at_the_right_margin()
    {
        var terminal = TerminalBuilder.With("0123456789ab");

        Assert.Equal("0123456789\nab", terminal.GetVisibleText());
        Assert.True(terminal.Screen[0].IsWrapped);
    }

    [Fact]
    public void Waits_to_wrap_until_the_next_character()
    {
        var terminal = TerminalBuilder.With("0123456789");

        Assert.Equal(0, terminal.Cursor.Row);
        Assert.True(terminal.Cursor.PendingWrap);
    }

    [Fact]
    public void Does_not_wrap_when_autowrap_is_off()
    {
        var terminal = TerminalBuilder.With("\e[?7l0123456789ab");

        Assert.Equal("012345678b", terminal.GetVisibleText());
    }

    [Fact]
    public void Gives_wide_characters_two_cells()
    {
        var terminal = TerminalBuilder.With("a中b");

        Assert.Equal("a中b", terminal.GetVisibleText());
        Assert.Equal(4, terminal.Cursor.Column);
        Assert.True(terminal.Screen[0][2].IsWideContinuation);
    }

    [Fact]
    public void Moves_a_wide_character_that_does_not_fit_to_the_next_line()
    {
        var terminal = TerminalBuilder.With("012345678中");

        Assert.Equal("012345678\n中", terminal.GetVisibleText());
    }

    [Fact]
    public void Attaches_combining_marks_to_the_previous_character()
    {
        var terminal = TerminalBuilder.With("éx");

        Assert.Equal("éx", terminal.GetVisibleText());
        Assert.Equal(2, terminal.Cursor.Column);
    }

    [Fact]
    public void Overwriting_half_of_a_wide_character_clears_the_other_half()
    {
        var terminal = TerminalBuilder.With("中\e[1Gx");

        Assert.Equal("x", terminal.GetVisibleText());
    }

    [Fact]
    public void Moves_the_cursor_to_an_absolute_position()
    {
        var terminal = TerminalBuilder.With("\e[3;4H");

        Assert.Equal((2, 3), (terminal.Cursor.Row, terminal.Cursor.Column));
    }

    [Fact]
    public void Keeps_the_cursor_inside_the_screen()
    {
        var terminal = TerminalBuilder.With("\e[99;99H");

        Assert.Equal((3, 9), (terminal.Cursor.Row, terminal.Cursor.Column));
    }

    [Fact]
    public void Moves_the_cursor_relatively()
    {
        var terminal = TerminalBuilder.With("\e[3;3H\e[A\e[2C");

        Assert.Equal((1, 4), (terminal.Cursor.Row, terminal.Cursor.Column));
    }

    [Fact]
    public void Erases_to_the_end_of_the_line()
    {
        var terminal = TerminalBuilder.With("abcdef\e[3G\e[K");

        Assert.Equal("ab", terminal.GetVisibleText());
    }

    [Fact]
    public void Erases_the_whole_display()
    {
        var terminal = TerminalBuilder.With("abc\r\ndef\e[2J");

        Assert.Equal(string.Empty, terminal.GetVisibleText());
    }

    [Fact]
    public void Erases_with_the_current_background()
    {
        var terminal = TerminalBuilder.With("\e[41m\e[2J");

        Assert.Equal(TerminalColor.Indexed(1), terminal.Screen[3][9].Style.Background);
    }

    [Fact]
    public void Scrolls_lines_into_the_scrollback()
    {
        var terminal = TerminalBuilder.With("1\r\n2\r\n3\r\n4\r\n5");

        Assert.Equal("2\n3\n4\n5", terminal.GetVisibleText());
        Assert.Equal("1", Assert.Single(terminal.Primary.Scrollback).GetText());
    }

    [Fact]
    public void Scrolls_only_inside_the_scroll_region()
    {
        var terminal = TerminalBuilder.With("top\e[2;3r\e[2;1Ha\r\nb\r\nc");

        Assert.Equal("top\nb\nc", terminal.GetVisibleText());
        Assert.Empty(terminal.Primary.Scrollback);
    }

    [Fact]
    public void Reverse_index_at_the_top_scrolls_down()
    {
        var terminal = TerminalBuilder.With("a\e[H\eM");

        Assert.Equal("\na", terminal.GetVisibleText());
    }

    [Fact]
    public void Inserts_and_deletes_characters()
    {
        var terminal = TerminalBuilder.With("abcd\e[2G\e[2@");

        Assert.Equal("a  bcd", terminal.GetVisibleText());

        terminal.Feed("\e[3P");

        Assert.Equal("acd", terminal.GetVisibleText());
    }

    [Fact]
    public void Inserts_and_deletes_lines()
    {
        var terminal = TerminalBuilder.With("1\r\n2\r\n3\e[2;1H\e[L");

        Assert.Equal("1\n\n2\n3", terminal.GetVisibleText());

        terminal.Feed("\e[2M");

        Assert.Equal("1\n3", terminal.GetVisibleText());
    }

    [Fact]
    public void Insert_mode_shifts_existing_text()
    {
        var terminal = TerminalBuilder.With("abc\e[1G\e[4hX");

        Assert.Equal("Xabc", terminal.GetVisibleText());
    }

    [Fact]
    public void Moves_to_tab_stops_every_eight_columns()
    {
        var terminal = TerminalBuilder.With("a\tb", columns: 20);

        Assert.Equal(9, terminal.Cursor.Column);
    }

    [Fact]
    public void Repeats_the_last_character()
    {
        var terminal = TerminalBuilder.With("x\e[3b");

        Assert.Equal("xxxx", terminal.GetVisibleText());
    }

    [Fact]
    public void Applies_basic_and_bright_colors()
    {
        var terminal = TerminalBuilder.With("\e[1;31;102mx");

        var style = terminal.Screen[0][0].Style;
        Assert.True(style.Has(CellAttributes.Bold));
        Assert.Equal(TerminalColor.Indexed(1), style.Foreground);
        Assert.Equal(TerminalColor.Indexed(10), style.Background);
    }

    [Fact]
    public void Applies_256_colors_and_true_color_with_semicolons()
    {
        var terminal = TerminalBuilder.With("\e[38;5;208;48;2;1;2;3mx");

        var style = terminal.Screen[0][0].Style;
        Assert.Equal(TerminalColor.Indexed(208), style.Foreground);
        Assert.Equal(TerminalColor.Rgb(1, 2, 3), style.Background);
    }

    [Fact]
    public void Applies_true_color_with_colons_and_a_color_space()
    {
        var terminal = TerminalBuilder.With("\e[38:2::10:20:30mx");

        Assert.Equal(TerminalColor.Rgb(10, 20, 30), terminal.Screen[0][0].Style.Foreground);
    }

    [Fact]
    public void Applies_styled_and_colored_underlines()
    {
        var terminal = TerminalBuilder.With("\e[4:3;58;5;9mx");

        var style = terminal.Screen[0][0].Style;
        Assert.Equal(UnderlineStyle.Curly, style.Underline);
        Assert.Equal(TerminalColor.Indexed(9), style.UnderlineColor);
    }

    [Fact]
    public void Resets_all_attributes()
    {
        var terminal = TerminalBuilder.With("\e[1;4;31m\e[mx");

        Assert.Equal(CellStyle.Default, terminal.Screen[0][0].Style);
    }

    [Fact]
    public void Switches_to_the_alternate_screen_and_back()
    {
        var terminal = TerminalBuilder.With("main\e[?1049hfull");

        Assert.True(terminal.IsAlternateScreen);
        Assert.Equal("    full", terminal.GetVisibleText());

        terminal.Feed("\e[?1049l");

        Assert.False(terminal.IsAlternateScreen);
        Assert.Equal("main", terminal.GetVisibleText());
        Assert.Equal(4, terminal.Cursor.Column);
    }

    [Fact]
    public void The_alternate_screen_keeps_no_scrollback()
    {
        var terminal = TerminalBuilder.With("\e[?1049h1\r\n2\r\n3\r\n4\r\n5");

        Assert.Empty(terminal.Alternate.Scrollback);
    }

    [Fact]
    public void Draws_line_graphics_from_the_dec_special_character_set()
    {
        var terminal = TerminalBuilder.With("\e(0lqk\e(Bx");

        Assert.Equal("┌─┐x", terminal.GetVisibleText());
    }

    [Fact]
    public void Saves_and_restores_the_cursor()
    {
        var terminal = TerminalBuilder.With("\e[2;3H\e[31m\e7\e[H\e[m\e8x");

        Assert.Equal(TerminalColor.Indexed(1), terminal.Screen[1][2].Style.Foreground);
    }

    [Fact]
    public void Reports_the_cursor_position()
    {
        var host = new RecordingHost();
        var terminal = TerminalBuilder.Small(host);

        terminal.Feed("\e[2;5H\e[6n");

        Assert.Equal("\e[2;5R", host.Replies);
    }

    [Fact]
    public void Answers_device_attribute_queries()
    {
        var host = new RecordingHost();
        var terminal = TerminalBuilder.Small(host);

        terminal.Feed("\e[c\e[>c");

        Assert.Equal("\e[?62;22c\e[>1;10;0c", host.Replies);
    }

    [Fact]
    public void Answers_mode_queries()
    {
        var host = new RecordingHost();
        var terminal = TerminalBuilder.Small(host);

        terminal.Feed("\e[?2004h\e[?2004$p\e[?9999$p");

        Assert.Equal("\e[?2004;1$y\e[?9999;0$y", host.Replies);
    }

    [Fact]
    public void Answers_only_valid_status_string_requests()
    {
        var host = new RecordingHost();
        var terminal = TerminalBuilder.Small(host);

        terminal.Feed("\eP$qr\e\\\eP$qevil\e\\");

        Assert.Equal("\eP1$r1;4r\e\\\eP0$r\e\\", host.Replies);
    }

    [Fact]
    public void Answers_color_queries()
    {
        var host = new RecordingHost();
        var terminal = TerminalBuilder.Small(host);

        terminal.Feed("\e]11;?\a");

        Assert.Equal("\e]11;rgb:1e1e/1e1e/1e1e\e\\", host.Replies);
    }

    [Fact]
    public void Never_reports_the_title()
    {
        var host = new RecordingHost();
        var terminal = TerminalBuilder.Small(host);

        terminal.Feed("\e]2;rm -rf ~\a\e[21t\e[20t");

        Assert.Equal(string.Empty, host.Replies);
    }

    [Fact]
    public void Sets_the_title()
    {
        var terminal = TerminalBuilder.With("\e]0;build: ok\a");

        Assert.Equal("build: ok", terminal.Title);
    }

    [Fact]
    public void Links_cells_to_hyperlinks()
    {
        var terminal = TerminalBuilder.With("\e]8;;https://example.com\aab\e]8;;\ac");

        Assert.Equal("https://example.com", terminal.GetHyperlink(terminal.Screen[0][0].HyperlinkId));
        Assert.Equal(0, terminal.Screen[0][2].HyperlinkId);
    }

    [Fact]
    public void Writes_to_the_clipboard_but_never_reads_it()
    {
        var host = new RecordingHost();
        var terminal = TerminalBuilder.Small(host);

        terminal.Feed("\e]52;c;aGVsbG8=\a\e]52;c;?\a");

        Assert.Equal(["hello"], host.Clipboard);
        Assert.Equal(string.Empty, host.Replies);
    }

    [Fact]
    public void Rings_the_bell()
    {
        var host = new RecordingHost();
        var terminal = TerminalBuilder.Small(host);

        terminal.Feed("\a");

        Assert.Equal(1, host.Bells);
    }

    [Fact]
    public void Records_a_command_from_shell_integration_markers()
    {
        var terminal = new Terminal(40, 5);

        terminal.Feed("\e]133;A\a$ \e]133;B\als -l\r\n\e]133;C\aoutput\r\n\e]133;D;2\a");

        var command = Assert.Single(terminal.Commands);
        Assert.Equal("ls -l", command.CommandLine);
        Assert.Equal(2, command.ExitCode);
        Assert.Equal(0, command.PromptLine);
        Assert.Equal(1, command.OutputLine);
        Assert.Equal(2, command.EndLine);
    }

    [Fact]
    public void Trusts_a_reported_command_line_only_with_the_session_nonce()
    {
        var terminal = new Terminal(40, 5) { ShellIntegrationNonce = "n0nce" };

        terminal.Feed("\e]633;A\a\e]633;B\a\e]633;E;echo hi;n0nce\a");
        var trusted = terminal.CurrentCommand!.CommandLineIsTrusted;
        terminal.Feed("\e]633;E;rm -rf /;wrong\a");

        Assert.True(trusted);
        Assert.Equal("rm -rf /", terminal.CurrentCommand!.CommandLine);
        Assert.False(terminal.CurrentCommand!.CommandLineIsTrusted);
    }

    [Fact]
    public void Records_who_ran_a_command()
    {
        var terminal = new Terminal(40, 5);

        terminal.Feed("\e]133;A\a\e]133;B\a");
        terminal.MarkNextCommandSource(CommandSource.Agent);
        terminal.Feed("ls\r\n\e]133;C\a");

        Assert.Equal(CommandSource.Agent, terminal.CurrentCommand!.Source);
    }

    [Fact]
    public void Knows_when_the_shell_waits_at_a_prompt()
    {
        var terminal = new Terminal(40, 5);

        terminal.Feed("\e]133;A\a$ \e]133;B\a");

        Assert.True(terminal.IsAtPrompt);
    }

    [Fact]
    public void Stamps_changed_rows_with_increasing_sequence_numbers()
    {
        var terminal = TerminalBuilder.With("a");
        var before = terminal.ChangeSequence;

        terminal.Feed("\r\nb");

        Assert.True(terminal.Screen[1].ChangedAt > before);
        Assert.True(terminal.Screen[0].ChangedAt <= before);
    }

    [Fact]
    public void Resizing_keeps_the_text_near_the_cursor()
    {
        var terminal = TerminalBuilder.With("1\r\n2\r\n3\r\n4");

        terminal.Resize(10, 2);

        Assert.Equal("3\n4", terminal.GetVisibleText());
        Assert.Equal(1, terminal.Cursor.Row);
    }

    [Fact]
    public void A_full_reset_clears_everything()
    {
        var terminal = TerminalBuilder.With("\e[31mtext\e[?1049h\ec");

        Assert.False(terminal.IsAlternateScreen);
        Assert.Equal(string.Empty, terminal.GetVisibleText());
        Assert.Equal(CellStyle.Default, terminal.Cursor.Style);
    }

    [Theory]
    [InlineData("file://mac.local/Users/ana/with%20space", "mac.local", "/Users/ana/with space")]
    [InlineData("file:///tmp", "", "/tmp")]
    [InlineData("file://pc/C:/Users/ana", "pc", "C:/Users/ana")]
    public void Decodes_the_working_folder_from_osc_7(string uri, string host, string path)
    {
        var terminal = TerminalBuilder.With($"\e]7;{uri}\a");

        Assert.Equal(path, terminal.WorkingDirectory);
        Assert.Equal(host, terminal.WorkingDirectoryHost);
    }

    [Theory]
    [InlineData("/tmp")]
    [InlineData("http://example.com/tmp")]
    [InlineData("file://host")]
    [InlineData("file:///tmp%0Aevil")]
    public void Ignores_a_malformed_working_folder(string uri)
    {
        var terminal = TerminalBuilder.With($"\e]7;{uri}\a");

        Assert.Null(terminal.WorkingDirectory);
    }

    [Fact]
    public void Keeps_text_above_the_cursor_when_shrinking()
    {
        var terminal = TerminalBuilder.With("one\r\ntwo");

        terminal.Resize(80, 5);

        Assert.StartsWith("one\ntwo", terminal.GetVisibleText(), StringComparison.Ordinal);
        Assert.Equal(1, terminal.Cursor.Row);
    }

    [Fact]
    public void Moves_rows_to_scrollback_when_the_cursor_would_fall_off()
    {
        var terminal = TerminalBuilder.With(string.Join("\r\n", Enumerable.Range(1, 10).Select(index => $"line {index}")));

        terminal.Resize(80, 4);

        Assert.StartsWith("line 7", terminal.GetVisibleText(), StringComparison.Ordinal);
        Assert.Equal(3, terminal.Cursor.Row);
        Assert.Equal(6, terminal.Primary.Scrollback.Count);
    }

    [Fact]
    public void Reads_lines_from_scrollback_and_screen()
    {
        var terminal = TerminalBuilder.With(string.Join("\r\n", Enumerable.Range(1, 30).Select(index => $"line {index}")));

        Assert.Equal("line 3\nline 4\nline 5", terminal.GetLineText(2, 4));
        Assert.Equal("line 29\nline 30", terminal.GetLineText(28, 29));
    }
}
