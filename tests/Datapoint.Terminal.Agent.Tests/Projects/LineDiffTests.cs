using Datapoint.Terminal.Agent.Projects;

namespace Datapoint.Terminal.Agent.Tests.Projects;

public sealed class LineDiffTests
{
    [Fact]
    public void Shows_changed_lines_with_context()
    {
        var before = string.Join('\n', Enumerable.Range(1, 20).Select(number => $"line {number}"));
        var after = before.Replace("line 6", "line six", StringComparison.Ordinal) + "\nline 21";

        string[] expected =
        [
            "  line 3", "  line 4", "  line 5", "- line 6", "+ line six", "  line 7", "  line 8", "  line 9",
            "…",
            "  line 18", "  line 19", "  line 20", "+ line 21",
        ];
        Assert.Equal(string.Join(Environment.NewLine, expected), LineDiff.Describe(before, after));
    }

    [Fact]
    public void Ignores_line_endings()
    {
        Assert.Equal(string.Empty, LineDiff.Describe("a\r\nb\r\n", "a\nb"));
    }

    [Fact]
    public void Shows_a_new_first_line()
    {
        Assert.Equal(string.Join(Environment.NewLine, "+ Use doas.", "  Be brief."), LineDiff.Describe("Be brief.", "Use doas.\nBe brief."));
    }
}
