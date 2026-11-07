using Datapoint.Terminal.Agent.Web;

namespace Datapoint.Terminal.Agent;

public interface IAgentUser
{
    // Approving must take a deliberate action that typing can't trigger.
    Task<CommandApproval> RequestApprovalAsync(CommandProposal proposal, CancellationToken cancellationToken);

    Task<string?> AskAsync(AgentQuestion question, CancellationToken cancellationToken);

    // Like approving a command, allowing must take a deliberate action: a button, never typed words.
    Task<WebAccess> RequestWebAccessAsync(WebAccessRequest request, CancellationToken cancellationToken);
}
