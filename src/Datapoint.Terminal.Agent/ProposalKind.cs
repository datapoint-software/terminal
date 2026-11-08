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

    // Command holds the rollout of a skill: its hosts, in order, and the health check; Purpose the skill's title and description. Nothing to edit.
    Rollout,

    // Look holds the hosts and what the agent wants to learn on each; Command the same as text. Nothing to edit.
    Look,

    // An offer to plan a task before doing it (product decision 114): Command holds its title, Purpose why a plan helps, and
    // Terminal the servers it will change, joined. Accepting switches the approval mode to Plan. Nothing to edit.
    PlanMode,
}
