using Datapoint.Terminal.Agent.Workspaces.History;

namespace Datapoint.Terminal.Shell;

// What the history window may do with a workspace's history. Failures are InvalidOperationException, with a message to show.
public interface IHistoryAccess
{
    int? RetentionDays { get; }

    IReadOnlyList<ConversationSummary> Conversations();

    IReadOnlyList<HistoryEntry> Conversation(long id);

    IReadOnlyList<HistoryEntry> Search(string words);

    IReadOnlyList<string> Connections();

    IReadOnlyList<ConnectionUsage> Usage();

    string WorkspaceName { get; }

    // Null clears everything.
    void Clear(string? connection);

    void SetRetentionDays(int? days);
}
