using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Agent.Tools;

internal static class QuestionOptions
{
    private const int MaxOptions = 6;

    // Plain strings are accepted too: some models flatten the objects, and older conversations used them.
    public static IReadOnlyList<AgentOption> Parse(AIFunctionArguments arguments)
    {
        if (!arguments.TryGetValue("options", out var value) || value is null)
        {
            return [];
        }

        var options = value switch
        {
            JsonElement { ValueKind: JsonValueKind.Array } element => element.EnumerateArray().Select(FromJson),
            IEnumerable<string> strings => strings.Select(label => new AgentOption(label)),
            _ => [],
        };
        return OnlyOneRecommended([.. options.Where(option => !string.IsNullOrWhiteSpace(option.Label)).Take(MaxOptions)]);
    }

    private static AgentOption FromJson(JsonElement item)
    {
        if (item.ValueKind == JsonValueKind.String)
        {
            return new AgentOption(item.GetString()!.Trim());
        }

        if (item.ValueKind != JsonValueKind.Object)
        {
            return new AgentOption(string.Empty);
        }

        var label = Text(item, "label") ?? string.Empty;
        var recommended = item.TryGetProperty("recommended", out var flag) && flag.ValueKind == JsonValueKind.True;
        return new AgentOption(label, Text(item, "description"), recommended);
    }

    private static string? Text(JsonElement item, string name) =>
        item.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String && property.GetString() is { } text && !string.IsNullOrWhiteSpace(text)
            ? text.Trim()
            : null;

    // A recommendation means one choice; when a model marks several, only the first counts.
    private static List<AgentOption> OnlyOneRecommended(List<AgentOption> options)
    {
        var first = options.FindIndex(option => option.IsRecommended);
        return [.. options.Select((option, index) => option.IsRecommended && index != first ? option with { IsRecommended = false } : option)];
    }
}
