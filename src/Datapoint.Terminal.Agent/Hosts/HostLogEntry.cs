namespace Datapoint.Terminal.Agent.Hosts;

public sealed record HostLogEntry(DateTimeOffset When, string Author, bool WithAgent, string Text, string Path);
