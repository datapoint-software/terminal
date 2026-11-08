namespace Datapoint.Terminal.Agent.Workspaces.History;

// Stored by name, so the order can change.
public enum HistoryEntryKind
{
    User,
    Agent,
    Command,
    Notice,

    // What a terminal printed between tasks, such as the user's own commands, sent to the model at the start of the next one;
    // Detail is the output and Text is empty.
    Output,
}
