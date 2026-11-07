namespace Datapoint.Terminal.Agent;

public enum ProposalKind
{
    RunCommand,

    // Command holds the connection's name; there's nothing to edit.
    OpenTerminal,

    // Command holds the keys in KeyNotation; they can be edited like a command.
    SendKeys,

    // Command holds the plan's numbered steps and how to roll back; Purpose its summary. Nothing to edit.
    Plan,

    // Command holds the diff to approve; the change is applied as the user sees it, with a backup first.
    EditFile,
}
