using Datapoint.Terminal.Agent.Workspaces;

namespace Datapoint.Terminal.Agent.Tests.Workspaces;

public sealed class WorkspaceTrustTests
{
    private const string Folder = "/home/ana/work";
    private static readonly string[] s_one = ["/home/ana/work/AGENTS.md"];
    private static readonly string[] s_two = ["/home/ana/work/AGENTS.md", "/home/ana/work/sub/TERMINAL.md"];

    private static WorkspaceTrustRecord Record(bool trusted, params string[] files) => new() { Folder = Folder, IsTrusted = trusted, InstructionFiles = files };

    [Fact]
    public void The_default_workspace_is_always_trusted() =>
        Assert.Equal(TrustState.Trusted, WorkspaceTrust.Evaluate(isDefault: true, hasConnections: true, s_one, Record(false)));

    [Fact]
    public void A_folder_with_nothing_to_trust_is_trusted_without_asking() =>
        Assert.Equal(TrustState.Trusted, WorkspaceTrust.Evaluate(isDefault: false, hasConnections: false, [], record: null));

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Asks_the_first_time_there_is_something_to_trust(bool hasFiles, bool hasConnections) =>
        Assert.Equal(TrustState.Ask, WorkspaceTrust.Evaluate(isDefault: false, hasConnections, hasFiles ? s_one : [], record: null));

    [Theory]
    [InlineData(true, TrustState.Trusted)]
    [InlineData(false, TrustState.Untrusted)]
    public void Keeps_the_decision_while_no_new_file_appears(bool trusted, TrustState expected) =>
        Assert.Equal(expected, WorkspaceTrust.Evaluate(isDefault: false, hasConnections: true, s_one, Record(trusted, s_two)));

    [Fact]
    public void Asks_again_when_a_new_instruction_file_appears() =>
        Assert.Equal(TrustState.Ask, WorkspaceTrust.Evaluate(isDefault: false, hasConnections: false, s_two, Record(true, s_one)));
}
