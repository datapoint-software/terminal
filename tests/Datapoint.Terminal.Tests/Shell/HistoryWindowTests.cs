using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Datapoint.Terminal.Agent.Workspaces.History;
using Datapoint.Terminal.Shell;

namespace Datapoint.Terminal.Tests.Shell;

public sealed class HistoryWindowTests
{
    private static readonly DateTimeOffset s_time = new(2026, 11, 6, 9, 30, 0, TimeSpan.Zero);

    [AvaloniaFact]
    public void Shows_a_conversation_when_it_is_selected()
    {
        var access = new FakeHistory();
        var window = new HistoryWindow(access) { Width = 900, Height = 600 };
        window.Show();
        var list = window.FindControl<ListBox>("List")!;
        list.SelectedIndex = 0;

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame();
        Directory.CreateDirectory("rendered");
        frame?.Save("rendered/history.png", new PngBitmapEncoderOptions());

        Assert.Equal(2, list.ItemCount);
        Assert.Equal(3, window.FindControl<StackPanel>("Entries")!.Children.Count);
    }

    [AvaloniaFact]
    public void Says_when_there_is_no_history()
    {
        var window = new HistoryWindow(new FakeHistory { IsEmpty = true });
        window.Show();

        Assert.Equal("No conversations yet. Conversations with the agent in this workspace are kept here.", window.FindControl<TextBlock>("Status")!.Text);
    }
}
