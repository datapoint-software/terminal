namespace Datapoint.Terminal.Agent;

// Warning is the agent's own note on what could go wrong, e.g. that a reboot ends the connection; the app adds its own.
// Purpose says why the step is needed. Title names the action in a few words, and Terminals the hosts it runs on, so the plan
// can show both apart from the command rather than in the agent's text. IsExample marks a command that only illustrates the
// step, e.g. when the exact package name isn't known yet.
public sealed record AgentPlanStep(string Command, string Purpose, string? Warning = null)
{
    public string? Title { get; init; }

    public IReadOnlyList<string> Terminals { get; init; } = [];

    public bool IsExample { get; init; }

    // By the hosts' names, not the list that holds them, so a revised plan's unchanged step compares equal.
    public bool Equals(AgentPlanStep? other) =>
        other is not null && string.Equals(Command, other.Command, StringComparison.Ordinal) && string.Equals(Purpose, other.Purpose, StringComparison.Ordinal)
        && string.Equals(Warning, other.Warning, StringComparison.Ordinal) && string.Equals(Title, other.Title, StringComparison.Ordinal)
        && IsExample == other.IsExample && Terminals.SequenceEqual(other.Terminals, StringComparer.OrdinalIgnoreCase);

    public override int GetHashCode() => HashCode.Combine(Command, Purpose, Title, Terminals.Count);
}
