using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using EmulatorTerminal = Datapoint.Terminal.Emulator.Terminal;

namespace Datapoint.Terminal.Controls.Tests;

public sealed class TerminalViewSelectionTests
{
    private static (Window Window, TerminalView View) Show(string output)
    {
        var terminal = new EmulatorTerminal(80, 24);
        terminal.Feed(output);
        var view = new TerminalView { Terminal = terminal, FontSize = 14 };
        var window = new Window { Width = 640, Height = 360, Content = view };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return (window, view);
    }

    private static Point Cell(TerminalView view, int column, int row) =>
        new((column * view.CellMetrics.Width) + 1, (row + 0.5) * view.CellMetrics.Height);

    [AvaloniaFact]
    public void Selects_by_dragging_across_lines()
    {
        var (window, view) = Show("first line\r\nsecond line\r\n");

        window.MouseDown(Cell(view, 6, 0), MouseButton.Left);
        window.MouseMove(Cell(view, 6, 1));
        window.MouseUp(Cell(view, 6, 1), MouseButton.Left);

        Assert.Equal("line\nsecond", view.SelectedText);
    }

    [AvaloniaFact]
    public void Selects_a_word_on_double_click()
    {
        var (window, view) = Show("tail -f /var/log/nginx/error.log\r\n");

        window.MouseDown(Cell(view, 12, 0), MouseButton.Left);
        window.MouseUp(Cell(view, 12, 0), MouseButton.Left);
        window.MouseDown(Cell(view, 12, 0), MouseButton.Left);
        window.MouseUp(Cell(view, 12, 0), MouseButton.Left);

        Assert.Equal("/var/log/nginx/error.log", view.SelectedText);
    }

    [AvaloniaFact]
    public void Finds_matches_from_newest_to_oldest_and_wraps()
    {
        var (_, view) = Show("error one\r\nfine\r\nan ERROR two\r\n");

        var newest = view.Find("error", older: true);
        view.Select(newest!.Value.Start, newest.Value.End);
        Assert.Equal("ERROR", view.SelectedText);

        var older = view.Find("error", older: true);
        view.Select(older!.Value.Start, older.Value.End);
        Assert.Equal(new TextPosition(0, 0), older.Value.Start);

        Assert.Equal(newest, view.Find("error", older: true));
        Assert.Equal(2, view.CountMatches("error"));
        Assert.Null(view.Find("missing", older: true));
    }
}
