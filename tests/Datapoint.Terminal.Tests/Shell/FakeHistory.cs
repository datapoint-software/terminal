using Datapoint.Terminal.Agent.Projects.History;
using Datapoint.Terminal.Shell;

namespace Datapoint.Terminal.Tests.Shell;

internal sealed class FakeHistory : IHistoryAccess
{
    private static readonly DateTimeOffset s_time = new(2026, 11, 6, 9, 30, 0, TimeSpan.Zero);

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

    public IReadOnlyList<ConnectionUsage> Usage() => IsEmpty ? [] : [new ConnectionUsage("db1", 7, 410_000), new ConnectionUsage("web1", 42, 3_100_000)];

    public string ProjectName => "shop";

    public List<string?> Cleared { get; } = [];

    public void Clear(string? connection) => Cleared.Add(connection);

    public void SetRetentionDays(int? days) => RetentionDays = days;
}
