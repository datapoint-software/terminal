using System.Text;

namespace Datapoint.Terminal.Agent;

public sealed partial class TerminalAgent
{
    // Lines are counted, not read, so this is cheap enough to refresh while the user types; IsCut is a guess from a typical
    // line length, and the message says only that long output is cut.
    public IReadOnlyList<PendingOutput> PendingOutput()
    {
        var pending = new List<PendingOutput>();
        foreach (var terminal in Terminals)
        {
            var lines = terminal.Terminal.GetState().NextOutputLine - terminal.SeenUpTo;
            if (lines > 0)
            {
                pending.Add(new PendingOutput(terminal.Name, lines, lines * 40 > OutputLimit));
            }
        }

        return pending;
    }

    // Every terminal's output since the agent last saw it, for the start of a task.
    private string NewOutputBlock()
    {
        var text = new StringBuilder();
        foreach (var terminal in Terminals)
        {
            var next = terminal.Terminal.GetState().NextOutputLine;
            var since = terminal.SeenUpTo;
            terminal.SeenUpTo = next;
            var output = next > since ? terminal.Terminal.GetTextSince(since) : string.Empty;
            if (output.Trim().Length == 0)
            {
                continue;
            }

            text.Append("[New output in \"").Append(terminal.Name).Append("\" since you last saw it:]\n").Append(Untrusted(output)).Append('\n');
        }

        return text.ToString();
    }

    private void MarkAllSeen()
    {
        foreach (var terminal in Terminals)
        {
            terminal.SeenUpTo = terminal.Terminal.GetState().NextOutputLine;
        }
    }
}
