namespace Datapoint.Terminal.Agent;

public interface IAgentUser
{
    // Approving must take a deliberate action that typing can't trigger.
    Task<CommandApproval> RequestApprovalAsync(CommandProposal proposal, CancellationToken cancellationToken);

    Task<string?> AskAsync(AgentQuestion question, CancellationToken cancellationToken);
}
