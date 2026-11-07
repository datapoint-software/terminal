namespace Datapoint.Terminal.Agent;

public sealed record AgentPlan(string Summary, IReadOnlyList<AgentPlanStep> Steps, string Rollback)
{
    // The plan as plain text, for the approval history and anything that can't show the steps themselves.
    public string Text =>
        string.Join('\n', Steps.Select((step, index) => $"{index + 1}. {step.Command}" + (step.Purpose.Length == 0 ? string.Empty : ": " + step.Purpose)))
        + (string.IsNullOrWhiteSpace(Rollback) ? string.Empty : "\n\nTo roll back: " + Rollback);
}
