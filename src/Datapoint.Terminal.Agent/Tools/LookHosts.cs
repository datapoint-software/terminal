using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Agent.Tools;

// The hosts of an ask_to_look call, each with the reason the agent gives; names only, matched to terminals by the agent.
internal static class LookHosts
{
    public static IReadOnlyList<(string Name, string Reason)> Parse(AIFunctionArguments arguments)
    {
        if (!arguments.TryGetValue("hosts", out var value) || value is not JsonElement { ValueKind: JsonValueKind.Array } hosts)
        {
            return [];
        }

        return [.. hosts.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Select(item => (Name: Text(item, "host"), Reason: Text(item, "reason")))
            .Where(item => item.Name.Length > 0)];
    }

    private static string Text(JsonElement item, string name) =>
        item.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? (property.GetString() ?? string.Empty).Trim() : string.Empty;
}
