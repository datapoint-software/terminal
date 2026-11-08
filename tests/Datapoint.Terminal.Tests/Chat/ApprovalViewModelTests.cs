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

    [Theory]
    [InlineData("patronictl list --format json", "patronictl list")]
    [InlineData("rm -rf build", null)]
    [InlineData("sudo patronictl list", null)]
    public void Offers_to_treat_a_command_counted_as_a_change_as_read_only(string command, string? expected)
    {
        var approval = new ApprovalViewModel(new CommandProposal(command, "Check the cluster", CommandRisk.Changes, "db1"));

        Assert.Equal(expected, approval.ReadOnlySuggestion);
        Assert.Equal(expected is not null, approval.HasReadOnlySuggestion);
    }

    [Fact]
    public void Offers_nothing_for_plans_keys_or_commands_the_check_counted_as_reading()
    {
        Assert.Null(new ApprovalViewModel(new CommandProposal("patronictl list", "x", CommandRisk.Changes, "db1") { Kind = ProposalKind.SendKeys }).ReadOnlySuggestion);
        Assert.Null(new ApprovalViewModel(new CommandProposal("ls", "x", CommandRisk.ReadOnly, "db1")).ReadOnlySuggestion);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Tells_what_was_approved_and_whether_it_was_taught(bool taught)
    {
        var approval = new ApprovalViewModel(new CommandProposal("patronictl list", "Check the cluster", CommandRisk.Changes, "db1"));
        (string Command, bool Taught)? approved = null;
        approval.ReadOnlyApproved = (command, isTaught) => approved = (command, isTaught);

        approval.Command = "vault status";
        approval.TreatAsReadOnly = taught;
        approval.RunCommand.Execute(null);

        Assert.Equal(("vault status", taught), approved);
        Assert.Equal(Strings.Format(Strings.ApprovalTreatAsReadOnly, "vault status"), approval.ReadOnlyOfferText);
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

    [Fact]
    public void A_plan_is_approved_as_a_whole_and_cannot_be_edited()
    {
        var proposal = new CommandProposal("1. apt-get install nginx", "Install nginx", CommandRisk.Changes, string.Empty) { Kind = ProposalKind.Plan };

        var approval = new ApprovalViewModel(proposal);

        Assert.True(approval.IsCommandReadOnly);
        Assert.Equal((Strings.ApprovalApprove, Strings.ApprovalHeadingPlan, Strings.ApprovalRiskPlan), (approval.RunLabel, approval.Heading, approval.RiskText));
        Assert.Equal(Strings.ApprovalPlanAnyTerminal, approval.TerminalText);
        Assert.False(approval.HasPlanText);
    }

    [Theory]
    [InlineData(2, false, "Step 2 of the approved plan.")]
    [InlineData(null, true, "Not in the approved plan: the agent needs a step it didn't plan for.")]
    public void Says_how_a_command_relates_to_the_approved_plan(int? step, bool isOutside, string expected)
    {
        var proposal = new CommandProposal("rm -rf /var/www", "Clean up", CommandRisk.Changes, "web1") { PlanStep = step, IsOutsidePlan = isOutside };

        Assert.Equal(expected, new ApprovalViewModel(proposal).PlanText);
    }
}
