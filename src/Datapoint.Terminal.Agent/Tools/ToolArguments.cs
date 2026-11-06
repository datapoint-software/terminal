using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Agent.Tools;

internal static class ToolArguments
{
    public static string? GetString(AIFunctionArguments arguments, string name)
    {
        if (!arguments.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            string text => text,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
            JsonElement { ValueKind: JsonValueKind.Null } => null,
            JsonElement element => element.GetRawText(),
            _ => value.ToString(),
        };
    }

    public static IReadOnlyList<string> GetStrings(AIFunctionArguments arguments, string name)
    {
        if (!arguments.TryGetValue(name, out var value) || value is null)
        {
            return [];
        }

        return value switch
        {
            JsonElement { ValueKind: JsonValueKind.Array } element => [.. element.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : item.GetRawText())],
            IEnumerable<string> strings => [.. strings],
            IEnumerable<object> items => [.. items.Select(item => item?.ToString() ?? string.Empty)],
            _ => [],
        };
    }
}
