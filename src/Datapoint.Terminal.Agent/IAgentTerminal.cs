namespace Datapoint.Terminal.Agent;

public interface IAgentTerminal
{
    event EventHandler? OutputArrived;

    event EventHandler? Ended;

    TerminalState GetState();

    CommandResult? GetCommand(long outputLine);

    string GetVisibleText();

    void TypeCommand(string text);

    // Parts as KeyNotation.Parse returns them, encoded for the program's current modes.
    void SendKeys(IReadOnlyList<string> parts);

    void Interrupt();
}
