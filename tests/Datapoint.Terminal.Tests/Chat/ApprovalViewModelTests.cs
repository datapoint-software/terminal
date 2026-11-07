using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Chat;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Tests.Chat;

public sealed class ApprovalViewModelTests
{
    [Fact]
    public void A_command_can_be_edited_and_run()
    {
        var approval = new ApprovalViewModel(new CommandProposal("df -h", "Check the disk", CommandRisk.ReadOnly, "web1", "production"));

        Assert.False(approval.IsCommandReadOnly);
        Assert.Equal(Strings.ApprovalRun, approval.RunLabel);
        Assert.Equal(Strings.ApprovalHeading, approval.Heading);
        Assert.Equal(Strings.Format(Strings.ApprovalInTerminalEnvironment, "web1", "production"), approval.TerminalText);
    }

    [Fact]
    public void Opening_a_terminal_names_the_connection_and_cannot_be_edited()
    {
        var proposal = new CommandProposal("web1", "Check the logs", CommandRisk.Changes, "web1", "production") { Kind = ProposalKind.OpenTerminal };

        var approval = new ApprovalViewModel(proposal);

        Assert.True(approval.IsCommandReadOnly);
        Assert.Equal(Strings.ApprovalOpen, approval.RunLabel);
        Assert.Equal(Strings.ApprovalHeadingOpen, approval.Heading);
        Assert.Equal(Strings.ApprovalRiskOpen, approval.RiskText);
        Assert.Equal(Strings.Format(Strings.ApprovalForConnectionEnvironment, "web1", "production"), approval.TerminalText);
    }

    [Fact]
    public void A_command_naming_an_ignored_file_says_so()
    {
        var file = Path.Combine(Path.GetTempPath(), "app", ".env");
        var proposal = new CommandProposal("cat .env", "Read settings", CommandRisk.ReadOnly, "Local") { IgnoredFile = file };

        var approval = new ApprovalViewModel(proposal);

        Assert.Equal(Strings.ApprovalHeadingIgnored, approval.Heading);
        Assert.Contains(".env", approval.RiskText, StringComparison.Ordinal);
    }
}
