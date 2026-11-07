using Datapoint.Terminal.Agent.Workspaces;

namespace Datapoint.Terminal.Agent.Tests.Workspaces;

public sealed class ConflictCopiesTests
{
    [Theory]
    [InlineData("SKILL (Ana's conflicted copy 2026-11-07).md", true)]
    [InlineData("SKILL-LAPTOP-7H2.md", true)]
    [InlineData("SKILL 2.md", true)]
    [InlineData("SKILL.sync-conflict-20261107-101010-ABCDEFG.md", true)]
    [InlineData("[Conflict] SKILL.md", true)]
    [InlineData("SKILL.md", false)]
    [InlineData("~$SKILL.md.0123456789ab.tmp", false)]
    [InlineData("SKILLS.md", false)]
    [InlineData("README.md", false)]
    [InlineData("SKILL (1).txt", false)]
    public void Recognizes_the_copies_sync_services_leave(string name, bool isCopy) =>
        Assert.Equal(isCopy, ConflictCopies.IsCopyOf(name, "SKILL.md"));
}
