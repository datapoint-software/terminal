using Datapoint.Terminal.Agent.Safety;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Agent;

// Product decision 129: a reply may carry several calls, which run in order and answer together, so a task costs fewer
// requests. Once a command in the reply fails, is declined or doesn't finish, the reply's remaining changes are skipped, so
// changes sent together still stop at the first failure as they would one at a time; checks still run.
public sealed partial class TerminalAgent
{
    // Tools that change a machine or the workspace's records of it; run_command only when the command isn't read-only.
    private static readonly HashSet<string> s_changingTools = new(StringComparer.Ordinal)
    {
        "edit_file", "send_keys", "host_done", "log_host_change", "update_host_docs",
    };

    // Set by a command that finished with exit code 0, or without one where the shell can't report it.
    private bool _commandSucceeded;

    private async Task RunCallsAsync(List<FunctionCallContent> calls, ChatMessage results, CancellationToken cancellationToken)
    {
        string? failed = null;
        foreach (var call in calls)
        {
            var result = IsRedirected ? SkippedForNewMessage
                : failed is not null && IsChange(call) ? SkippedAfterFailure(failed)
                : await InvokeCallAsync(call, cancellationToken).ConfigureAwait(false);
            if (failed is null && IsCommand(call) && !_commandSucceeded && !IsRedirected)
            {
                failed = CommandOf(call);
            }

            NoteSeen(call, result);
            NoteHistoryId(call);
            results.Contents.Add(new FunctionResultContent(call.CallId, result));
        }
    }

    private async Task<string> InvokeCallAsync(FunctionCallContent call, CancellationToken cancellationToken)
    {
        _commandSucceeded = false;
        _historyId = null;
        return await InvokeToolAsync(call, cancellationToken).ConfigureAwait(false);
    }

    private static bool IsCommand(FunctionCallContent call) => string.Equals(call.Name, "run_command", StringComparison.Ordinal);

    private bool IsChange(FunctionCallContent call) => s_changingTools.Contains(call.Name)
        || (IsCommand(call) && CommandClassifier.Classify(CommandOf(call), LearnedCommands) != CommandRisk.ReadOnly);

    private static string CommandOf(FunctionCallContent call) =>
        call.Arguments?.TryGetValue("command", out var command) == true ? command?.ToString() ?? string.Empty : string.Empty;

    private static string SkippedAfterFailure(string failed) =>
        $"Not run: `{failed}` earlier in this reply failed, wasn't run or didn't finish, so the reply's remaining changes were skipped. "
        + "Decide what to do from its result before going on.";
}
