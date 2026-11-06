namespace Datapoint.Terminal.Agent;

public enum ApprovalMode
{
    /// <summary>Every command is proposed and waits for the user's approval.</summary>
    Ask,

    /// <summary>Read-only commands run on their own; anything that may change the system asks.</summary>
    AskForRisky,

    /// <summary>Commands run without asking, except those that always ask; lasts for one task.</summary>
    Auto,
}
