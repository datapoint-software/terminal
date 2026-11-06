namespace Datapoint.Terminal.Agent;

/// <summary>The terminal as the agent sees it: what's on it, and a way to type into it in the user's view.</summary>
public interface IAgentTerminal
{
    /// <summary>Raised on a background thread whenever new output has been processed.</summary>
    event EventHandler? OutputArrived;

    /// <summary>Raised on a background thread when the session ends.</summary>
    event EventHandler? Ended;

    TerminalState GetState();

    /// <summary>The finished or running command started at <paramref name="outputLine"/>, if the shell reports commands.</summary>
    CommandResult? GetCommand(long outputLine);

    string GetVisibleText();

    /// <summary>Types <paramref name="text"/> as if from the keyboard, and records that the next command is the agent's.</summary>
    void TypeCommand(string text);

    /// <summary>Sends an interrupt (Ctrl+C) to whatever is running.</summary>
    void Interrupt();
}
