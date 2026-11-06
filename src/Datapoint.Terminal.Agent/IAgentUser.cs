namespace Datapoint.Terminal.Agent;

/// <summary>What the agent needs from the person at the keyboard. Implemented by the chat pane.</summary>
public interface IAgentUser
{
    /// <summary>Asks the user to approve a command. Approving must take a deliberate action that typing can't trigger.</summary>
    Task<CommandApproval> RequestApprovalAsync(CommandProposal proposal, CancellationToken cancellationToken);

    /// <summary>Asks the user a question in the chat; null when the user declined to answer.</summary>
    Task<string?> AskAsync(AgentQuestion question, CancellationToken cancellationToken);
}
