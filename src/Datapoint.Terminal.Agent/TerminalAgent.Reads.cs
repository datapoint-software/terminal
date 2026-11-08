namespace Datapoint.Terminal.Agent;

// Product decision 136: reading a terminal's screen again when nothing on it has changed returns a line saying so, not the
// same screen again, as long as the earlier read is still in the conversation. Waiting on a service or a prompt otherwise
// sends the same screen with every check, and resends each copy with every reply after it.
public sealed partial class TerminalAgent
{
    private readonly Dictionary<string, string> _lastScreens = new(StringComparer.OrdinalIgnoreCase);

    // Null when the screen differs from the last one read in this conversation, or there's none.
    private string? UnchangedScreen(AgentTerminal terminal, string screen)
    {
        lock (_lastScreens)
        {
            var unchanged = _lastScreens.TryGetValue(terminal.Name, out var last) && string.Equals(last, screen, StringComparison.Ordinal);
            _lastScreens[terminal.Name] = screen;
            return unchanged ? $"The screen of \"{terminal.Name}\" is unchanged since your last read_screen of it." : null;
        }
    }

    // A new conversation no longer has the earlier reads.
    private void ForgetScreens()
    {
        lock (_lastScreens)
        {
            _lastScreens.Clear();
        }
    }
}
