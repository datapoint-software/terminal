namespace Datapoint.Terminal.Agent.Workspaces;

// Before and After are masked like the stored snapshots. When only values that look like secrets changed, they're equal.
public sealed record InstructionChange(string Path, string Before, string After)
{
    public bool IsOnlySecrets => string.Equals(Before, After, StringComparison.Ordinal);
}
