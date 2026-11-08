using Datapoint.Terminal.Agent.Skills;
using Datapoint.Terminal.Agent.Tools;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Agent;

// Product decision 131: once a rollout moves past a host that finished and stayed healthy, the output its commands returned is
// left out of the conversation, keeping each command, its exit code and the agent's summary of the host. Everything stays in
// the terminal, the chat and the history; only what's sent to the model again with every later reply gets shorter.
public sealed partial class TerminalAgent
{
    private const string OutputStart = "<terminal_output>";

    private readonly Dictionary<string, string?> _hostSummaries = new(StringComparer.OrdinalIgnoreCase);

    private void NoteHostSummary(string host, AIFunctionArguments arguments) => _hostSummaries[host] = ToolArguments.GetString(arguments, "summary")?.Trim();

    private void TrimFinishedHosts(IEnumerable<RolloutHost> hosts)
    {
        foreach (var host in hosts.Where(host => host.State == RolloutHostState.Done))
        {
            var names = Terminals.Where(terminal => string.Equals(terminal.Name, host.Name, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(terminal.Connection, host.Name, StringComparison.OrdinalIgnoreCase))
                .Select(terminal => terminal.Name)
                .Append(host.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var calls = _history.SelectMany(message => message.Contents).OfType<FunctionCallContent>()
                .Where(call => call.Arguments?.TryGetValue("terminal", out var terminal) == true && terminal?.ToString() is { } name && names.Contains(name))
                .Select(call => call.CallId)
                .ToHashSet(StringComparer.Ordinal);
            TrimResults(calls, LeftOut(host.Name, _hostSummaries.GetValueOrDefault(host.Name)));
        }
    }

    private void TrimResults(HashSet<string> calls, string note)
    {
        foreach (var contents in _history.Select(message => message.Contents))
        {
            for (var index = 0; index < contents.Count; index++)
            {
                if (contents[index] is FunctionResultContent { Result: string text } result && calls.Contains(result.CallId)
                    && text.IndexOf(OutputStart, StringComparison.Ordinal) is var start and >= 0)
                {
                    contents[index] = new FunctionResultContent(result.CallId, text[..start] + note);
                }
            }
        }
    }

    private static string LeftOut(string host, string? summary) =>
        $"[Output left out once {host} was done and healthy, to keep the conversation short."
        + (string.IsNullOrEmpty(summary) ? "]" : $" Your summary then: {summary}]");
}
