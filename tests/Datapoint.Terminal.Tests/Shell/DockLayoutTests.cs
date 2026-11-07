using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Datapoint.Terminal.Shell;

namespace Datapoint.Terminal.Tests.Shell;

public sealed class DockLayoutTests
{
    private readonly Border _chat = new() { Background = Brushes.LightBlue };
    private readonly Border _terminals = new() { Background = Brushes.Black };
    private readonly Border _separator = new() { Background = Brushes.Gray };
    private readonly GridSplitter _splitter = new();

    private Window Show(TerminalSide side, bool isHidden)
    {
        var body = new Grid { Children = { _terminals, _separator, _splitter, _chat } };
        new DockLayout(body, _chat, _terminals, _separator, _splitter).Apply(side, isHidden, 300, 200);
        var window = new Window { Width = 1000, Height = 700, Content = body };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaTheory]
    [InlineData(TerminalSide.Right, 700, 0, 300, 700)]
    [InlineData(TerminalSide.Left, 0, 0, 300, 700)]
    [InlineData(TerminalSide.Bottom, 0, 500, 1000, 200)]
    public void Docks_the_terminals_to_each_side_and_gives_the_chat_the_rest(TerminalSide side, double x, double y, double width, double height)
    {
        var window = Show(side, isHidden: false);

        Assert.Equal(new Avalonia.Rect(x, y, width, height), _terminals.Bounds);
        Assert.Equal(side == TerminalSide.Bottom ? 499 : 699, side == TerminalSide.Bottom ? _chat.Bounds.Height : _chat.Bounds.Width);
        window.Close();
    }

    [AvaloniaFact]
    public void Gives_the_chat_everything_when_the_terminals_are_hidden()
    {
        var window = Show(TerminalSide.Right, isHidden: true);

        Assert.False(_terminals.IsVisible);
        Assert.Equal(1000, _chat.Bounds.Width);
        window.Close();
    }
}
