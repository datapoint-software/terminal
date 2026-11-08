using System.Globalization;
using System.Text;
using Datapoint.Terminal.Agent.Tools;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Agent;

// Product decisions 132 and 133: the conversation sent to the model starts afresh when a rollout moves on to its next batch,
// and is summarized when it grows past CompactAtTokens. Each reply resends the whole conversation, so dropping what a later
// step no longer needs saves on every reply after it. What the next step does need is carried into the new conversation: the
// user's messages, the files listed in .terminalignore, the skill being rolled out, the approved plan, and what the agent
// noted about the hosts it finished. The terminals, the chat and the history keep everything.
public sealed partial class TerminalAgent
{
    private const int CarriedMessages = 10;
    private const int CarriedMessageCharacters = 4_000;

    // The user's messages in this conversation, oldest first: the first, which usually says what the task is, and the latest.
    private readonly List<string> _userMessages = [];

    private readonly Dictionary<string, string> _hostSummaries = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _hostNotes = [];

    // What opened the conversation before the user's words: .terminalignore patterns, and untrusted instruction files as data.
    private string _preface = string.Empty;

    // Set by host_done when the rollout moves on to its next batch: what it told the model, carried into the fresh conversation.
    private string? _nextBatch;

    private void NoteUserWords(string message)
    {
        _userMessages.Add(message.Length <= CarriedMessageCharacters ? message : message[..CarriedMessageCharacters] + " [cut short]");
        if (_userMessages.Count <= CarriedMessages)
        {
            return;
        }

        _userMessages.RemoveAt(1);
    }

    private void NoteHostDone(string host, AIFunctionArguments arguments)
    {
        if (ToolArguments.GetString(arguments, "summary")?.Trim() is { Length: > 0 } summary)
        {
            _hostSummaries[host] = summary;
        }

        if (ToolArguments.GetString(arguments, "notes")?.Trim() is not { Length: > 0 } notes)
        {
            return;
        }

        _hostNotes.Add($"From {host}: {notes}");
    }

    private void ForgetCarried()
    {
        _userMessages.Clear();
        _hostSummaries.Clear();
        _hostNotes.Clear();
        _preface = string.Empty;
        _nextBatch = null;
    }

    private void StartNextBatchAfresh()
    {
        if (_nextBatch is not { } next)
        {
            return;
        }

        _nextBatch = null;
        StartAfresh(
            "[This conversation was started afresh when the rollout moved on to its next batch, to keep it short. The terminals, "
            + "the chat and the history still have everything; search_history finds earlier commands and their output.]\n"
            + Carried()
            + "\nThe rollout now: " + next + "\n");
    }

    private void StartAfresh(string message)
    {
        lock (_sync)
        {
            _history.RemoveRange(1, _history.Count - 1);
            ForgetScreens();
            _history.Add(new ChatMessage(ChatRole.User, DescribeTerminals() + message));
        }

        Usage.ForgetContext();
        Raise(new AgentEventArgs(AgentEventKind.UsageChanged, string.Empty));
    }

    // What the next step needs from the conversation it replaces.
    private string Carried()
    {
        var text = new StringBuilder(_preface);
        if (_userMessages.Count > 0)
        {
            text.Append("The user's messages in this conversation, oldest first:\n");
            foreach (var message in _userMessages)
            {
                text.Append("<user_message>\n").Append(message.Replace("</user_message>", "</user_message_>", StringComparison.OrdinalIgnoreCase)).Append("\n</user_message>\n");
            }
        }

        if (_plan is { } plan)
        {
            text.Append("\nThe plan the user approved for this task:\n").Append(plan.Text).Append('\n');
        }

        return text.Append(CarriedRollout()).ToString();
    }

    private string CarriedRollout()
    {
        if (_run is not { IsOver: false } run || Skills is not { } folder)
        {
            return string.Empty;
        }

        var text = new StringBuilder("\nThe skill being rolled out, as read_skill gave it:\n").Append(SkillText(run.Skill, folder)).Append('\n');
        if (_hostSummaries.Count > 0)
        {
            text.Append("\nHosts finished so far, with your summary of each:\n");
            foreach (var (host, summary) in _hostSummaries)
            {
                text.Append(CultureInfo.InvariantCulture, $"- {host}: {summary}\n");
            }
        }

        if (_hostNotes.Count > 0)
        {
            text.Append("\nYour notes for the hosts still to come:\n");
            foreach (var note in _hostNotes)
            {
                text.Append("- ").Append(note).Append('\n');
            }
        }

        return text.ToString();
    }
}
