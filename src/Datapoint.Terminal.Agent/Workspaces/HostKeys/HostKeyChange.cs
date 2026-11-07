namespace Datapoint.Terminal.Agent.Workspaces.HostKeys;

// Before and Now are "type fingerprint".
public sealed record HostKeyChange(string Connection, string Host, int Port, string Before, string Now);
