namespace Datapoint.Terminal.Agent;

// New output in a terminal that would go with the user's next message.
public sealed record PendingOutput(string Terminal, long Lines, bool IsCut);
