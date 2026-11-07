using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Datapoint.Terminal.Shell;

namespace Datapoint.Terminal.Tests.Shell;

public sealed class ChatLayoutTests
{
    private readonly Border _chat = new() { Background = Brushes.LightBlue };
    private readonly Border _terminals = new() { Background = Brushes.Black };
    private readonly Border _separator = new() { Background = Brushes.Gray };
    private readonly GridSplitter _splitter = new();

    private Window Show(ChatSide side, bool isHidden)
    {
        var body = new Grid { Children = { _terminals, _separator, _splitter, _chat } };
        new ChatLayout(body, _chat, _terminals, _separator, _splitter).Apply(side, isHidden, 300, 200);
        var window = new Window { Width = 1000, Height = 700, Content = body };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaTheory]
    [InlineData(ChatSide.Right, 699, 0, 300, 700)]
    [InlineData(ChatSide.Left, 0, 0, 300, 700)]
    [InlineData(ChatSide.Bottom, 0, 499, 1000, 200)]
    [InlineData(ChatSide.Top, 0, 0, 1000, 200)]
    public void Docks_the_chat_to_each_side(ChatSide side, double x, double y, double width, double height)
    {
        var window = Show(side, isHidden: false);

        Assert.Equal(new Avalonia.Rect(x + (side == ChatSide.Right ? 1 : 0), y + (side == ChatSide.Bottom ? 1 : 0), width, height), _chat.Bounds);
        window.Close();
    }

    [AvaloniaFact]
    public void Gives_the_terminals_everything_when_hidden()
    {
        var window = Show(ChatSide.Right, isHidden: true);

        Assert.False(_chat.IsVisible);
        Assert.Equal(1000, _terminals.Bounds.Width);
        window.Close();
    }
}
