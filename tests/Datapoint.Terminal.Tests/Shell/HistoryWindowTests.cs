using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Datapoint.Terminal.Agent.Projects.History;
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

        Assert.Equal("No conversations yet. Conversations with the agent in this project are kept here.", window.FindControl<TextBlock>("Status")!.Text);
    }

    private sealed class FakeHistory : IHistoryAccess
    {
        public bool IsEmpty { get; init; }

        public int? RetentionDays { get; private set; } = 90;

        public IReadOnlyList<ConversationSummary> Conversations() => IsEmpty
            ? []
            : [new ConversationSummary(2, s_time, "Why is web1 slow since this morning?", 3), new ConversationSummary(1, s_time.AddDays(-1), "Renew the certificate on web1", 8)];

        public IReadOnlyList<HistoryEntry> Conversation(long id) =>
        [
            new HistoryEntry(id, s_time, HistoryEntryKind.User, "Why is web1 slow since this morning?"),
            new HistoryEntry(id, s_time.AddSeconds(5), HistoryEntryKind.Command, "uptime", "web1", " 09:30:05 up 12 days, load average: 7.90, 6.12, 4.01", 0, "web1"),
            new HistoryEntry(id, s_time.AddSeconds(9), HistoryEntryKind.Agent, "The load average is high. Something started using the CPU around 8:00."),
        ];

        public IReadOnlyList<HistoryEntry> Search(string words) => [];

        public IReadOnlyList<string> Connections() => ["web1"];

        public void Clear(string? connection)
        {
        }

        public void SetRetentionDays(int? days) => RetentionDays = days;
    }
}
