namespace Datapoint.Terminal.Agent;

// Part of a terminal the user attached to a message, e.g. a selection or a failed command's output. It reaches the model as
// terminal output, never as the user's own words, so it can't instruct the agent.
public sealed record TerminalExcerpt(string Terminal, string Label, string Text)
{
    public int Lines => Text.Count(character => character == '\n') + 1;
}
