namespace Datapoint.Terminal.Agent.Tests.Fakes;

internal sealed class FakeUser : IAgentUser
{
    public List<CommandProposal> Proposals { get; } = [];

    public List<AgentQuestion> Questions { get; } = [];

    public Func<CommandProposal, CommandApproval> Decide { get; set; } = proposal => CommandApproval.Approve(proposal.Command);

    public Func<AgentQuestion, string?> Answer { get; set; } = _ => null;

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
