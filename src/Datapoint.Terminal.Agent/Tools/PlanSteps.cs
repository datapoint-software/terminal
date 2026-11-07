using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Agent.Tools;

internal static class PlanSteps
{
    // A plain string is taken as the command alone: some models flatten the objects, and splitting "command: why" would
    // break commands that contain a colon.
    public static IReadOnlyList<AgentPlanStep> Parse(AIFunctionArguments arguments)
    {
        if (!arguments.TryGetValue("steps", out var value) || value is null)
        {
            return [];
        }

        var steps = value switch
        {
            JsonElement { ValueKind: JsonValueKind.Array } element => element.EnumerateArray().Select(FromJson),
            IEnumerable<string> strings => strings.Select(command => new AgentPlanStep(command.Trim(), string.Empty)),
            _ => [],
        };
        return [.. steps.Where(step => step.Command.Length > 0 || step.Purpose.Length > 0)];
    }

    private static AgentPlanStep FromJson(JsonElement item) => item.ValueKind switch
    {
        JsonValueKind.String => new AgentPlanStep(item.GetString()!.Trim(), string.Empty),
        JsonValueKind.Object => new AgentPlanStep(Text(item, "command") ?? string.Empty, Text(item, "purpose") ?? string.Empty, Text(item, "warning")),
        _ => new AgentPlanStep(string.Empty, string.Empty),
    };

    private static string? Text(JsonElement item, string name) =>
        item.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String && property.GetString() is { } text && !string.IsNullOrWhiteSpace(text)
            ? text.Trim()
            : null;
}
