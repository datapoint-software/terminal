namespace Datapoint.Terminal.Agent;

public enum ProposalKind
{
    RunCommand,

    // Command holds the connection's name; there's nothing to edit.
    OpenTerminal,

    // Command holds the keys in KeyNotation; they can be edited like a command.
    SendKeys,
}
