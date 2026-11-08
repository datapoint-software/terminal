using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Agent;

// Product decision 139: a long output the agent read many replies ago is replaced, in what's sent to the model, by a line
// naming the history entry that keeps it, which read_history reads back. The command and its exit code stay. Changing earlier
// messages means the provider's cache has to be written again from there, so it happens only once enough has built up to be
// worth it, not at every step. Output with no history entry, such as in a read-only window, is left as it is.
public sealed partial class TerminalAgent
{
    private const string OutputStart = "<terminal_output>";

    // Replies after a result before its output can be left out.
    private const int OldAfterReplies = 8;

    // Characters an output must have to be left out, and in all for a sweep to be worth a new cache write.
    private const int LongOutput = 2_000;
    private const int WorthSweeping = 20_000;

    // The history entry each command call's output was kept as, by call.
    private readonly Dictionary<string, long> _historyIds = new(StringComparer.Ordinal);

    // Set while a call runs, by the command it finished.
    private long? _historyId;

    private void NoteHistoryId(FunctionCallContent call)
    {
        if (_historyId is not { } id)
        {
            return;
        }

        _historyIds[call.CallId] = id;
    }

    private void LeaveOutOldOutput()
    {
        var old = OldOutputs();
        if (old.Sum(item => item.Length) < WorthSweeping)
        {
            return;
        }

        foreach (var (contents, index, result, start, id) in old.Select(item => (item.Contents, item.Index, item.Result, item.Start, item.Id)))
        {
            contents[index] = new FunctionResultContent(result.CallId, ((string)result.Result!)[..start] + LeftOut(id));
        }
    }

    private List<(IList<AIContent> Contents, int Index, FunctionResultContent Result, int Start, long Id, int Length)> OldOutputs()
    {
        var old = new List<(IList<AIContent>, int, FunctionResultContent, int, long, int)>();
        var replies = 0;
        for (var message = _history.Count - 1; message > 0; message--)
        {
            var contents = _history[message].Contents;
            replies += _history[message].Role == ChatRole.Assistant ? 1 : 0;
            for (var index = 0; index < contents.Count && replies >= OldAfterReplies; index++)
            {
                if (contents[index] is FunctionResultContent { Result: string text } result && _historyIds.TryGetValue(result.CallId, out var id)
                    && text.IndexOf(OutputStart, StringComparison.Ordinal) is var start and >= 0 && text.Length - start >= LongOutput)
                {
                    old.Add((contents, index, result, start, id, text.Length - start));
                }
            }
        }

        return old;
    }

    private static string LeftOut(long id) =>
        $"[Output left out to keep the conversation short. It's kept as history entry #{id}: read_history with entry {id} reads it, "
        + "by line range or the lines containing some text.]";
}
