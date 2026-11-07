using Datapoint.Terminal.Shell;

namespace Datapoint.Terminal.Tests.Shell;

public sealed class WindowTitlesTests
{
    [Fact]
    public void Names_the_workspace() =>
        Assert.Equal("api — Datapoint Terminal", WindowTitles.For("api", shellTitle: null, needsUser: false));

    [Fact]
    public void Adds_the_shell_title() =>
        Assert.Equal("api — vim — Datapoint Terminal", WindowTitles.For("api", "vim", needsUser: false));

    [Fact]
    public void Puts_the_waiting_note_first_so_truncation_keeps_it() =>
        Assert.StartsWith("Waiting for you — api", WindowTitles.For("api", "vim", needsUser: true), StringComparison.Ordinal);
}
