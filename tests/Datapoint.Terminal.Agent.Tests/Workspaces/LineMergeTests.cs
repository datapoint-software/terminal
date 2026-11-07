using Datapoint.Terminal.Agent.Workspaces;

namespace Datapoint.Terminal.Agent.Tests.Workspaces;

public sealed class LineMergeTests
{
    private const string Common = "# Patch\n\n1. Drain.\n2. Upgrade.\n3. Reboot.\n4. Undrain.\n";

    [Fact]
    public void Joins_changes_to_different_lines()
    {
        var ours = Common.Replace("1. Drain.", "1. Drain the node.", StringComparison.Ordinal);
        var theirs = Common.Replace("4. Undrain.", "4. Undrain.\n5. Check the logs.", StringComparison.Ordinal);

        Assert.Equal("# Patch\n\n1. Drain the node.\n2. Upgrade.\n3. Reboot.\n4. Undrain.\n5. Check the logs.\n", LineMerge.TryMerge(Common, ours, theirs));
    }

    [Fact]
    public void Takes_the_same_change_made_on_both_sides_once()
    {
        var both = Common.Replace("3. Reboot.", "3. Reboot, then wait.", StringComparison.Ordinal);

        Assert.Equal(both, LineMerge.TryMerge(Common, both, both));
    }

    [Theory]
    [InlineData("2. Upgrade everything.", "2. Upgrade nginx.")]
    [InlineData("2. Upgrade.\n2b. Wait.", "2. Upgrade.\n2c. Check.")]
    public void Gives_up_when_both_change_the_same_place(string mine, string other)
    {
        var ours = Common.Replace("2. Upgrade.", mine, StringComparison.Ordinal);
        var theirs = Common.Replace("2. Upgrade.", other, StringComparison.Ordinal);

        Assert.Null(LineMerge.TryMerge(Common, ours, theirs));
    }
}
