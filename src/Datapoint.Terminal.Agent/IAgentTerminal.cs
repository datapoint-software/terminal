namespace Datapoint.Terminal.Agent;

public interface IAgentTerminal
{
    event EventHandler? OutputArrived;

    event EventHandler? Ended;

    TerminalState GetState();

    CommandResult? GetCommand(long outputLine);

    string GetVisibleText();

    void TypeCommand(string text);

    void Interrupt();
}
