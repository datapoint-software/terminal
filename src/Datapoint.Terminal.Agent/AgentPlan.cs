namespace Datapoint.Terminal.Agent;

public sealed record AgentPlan(string Summary, IReadOnlyList<string> Steps, string Rollback)
{
    // The plan as the user reads it in the approval card and in the history.
    public string Text =>
        string.Join('\n', Steps.Select((step, index) => $"{index + 1}. {step}"))
        + (string.IsNullOrWhiteSpace(Rollback) ? string.Empty : "\n\nTo roll back: " + Rollback);
}
