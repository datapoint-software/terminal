using Datapoint.Terminal.Agent.Projects.History;

namespace Datapoint.Terminal.Shell;

// What the history window may do with a project's history. Failures are InvalidOperationException, with a message to show.
public interface IHistoryAccess
{
    int? RetentionDays { get; }

    IReadOnlyList<ConversationSummary> Conversations();

    IReadOnlyList<HistoryEntry> Conversation(long id);

    IReadOnlyList<HistoryEntry> Search(string words);

    IReadOnlyList<string> Connections();

    // Null clears everything.
    void Clear(string? connection);

    void SetRetentionDays(int? days);
}
