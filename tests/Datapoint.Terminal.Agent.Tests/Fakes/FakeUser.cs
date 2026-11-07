using Datapoint.Terminal.Agent.Web;

namespace Datapoint.Terminal.Agent.Tests.Fakes;

internal sealed class FakeUser : IAgentUser
{
    public List<CommandProposal> Proposals { get; } = [];

    public List<AgentQuestion> Questions { get; } = [];

    public Func<CommandProposal, CommandApproval> Decide { get; set; } = proposal => CommandApproval.Approve(proposal.Command);

    public Func<AgentQuestion, string?> Answer { get; set; } = _ => null;

    public List<WebAccessRequest> WebRequests { get; } = [];

    public Func<WebAccessRequest, WebAccess> AllowWeb { get; set; } = _ => WebAccess.Once;

    public Task<WebAccess> RequestWebAccessAsync(WebAccessRequest request, CancellationToken cancellationToken)
    {
        WebRequests.Add(request);
        return Task.FromResult(AllowWeb(request));
    }

    public Task<CommandApproval> RequestApprovalAsync(CommandProposal proposal, CancellationToken cancellationToken)
    {
        Proposals.Add(proposal);
        return Task.FromResult(Decide(proposal));
    }

    public Task<string?> AskAsync(AgentQuestion question, CancellationToken cancellationToken)
    {
        Questions.Add(question);
        return Task.FromResult(Answer(question));
    }
}
