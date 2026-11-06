using Datapoint.Terminal.Markdown;

namespace Datapoint.Terminal.Tests.Markdown;

public sealed class SyntaxHighlighterTests
{
    [Theory]
    [InlineData("bash", "ls -la $HOME # list\necho \"hi\"")]
    [InlineData("python", "def f(x):\n    return x * 2  # double")]
    [InlineData("json", "{ \"a\": [1, true, null] }")]
    [InlineData("yaml", "key: value # note\nlist:\n  - 1")]
    [InlineData("sql", "SELECT * FROM t WHERE a = 'x'")]
    [InlineData("diff", "--- a\n+++ b\n@@ -1 +1 @@\n-old\n+new")]
    [InlineData("unknown", "anything")]
    public void Keeps_the_code_exactly(string language, string code) =>
        Assert.Equal(code, string.Concat(SyntaxHighlighter.Highlight(code, language).Select(token => token.Text)));

    [Fact]
    public void Colours_shell_pieces()
    {
        var tokens = SyntaxHighlighter.Highlight("if [ -n \"$X\" ]; then echo $HOME; fi # done", "sh");

        Assert.Contains(("if", SyntaxKind.Keyword), tokens);
        Assert.Contains(("\"$X\"", SyntaxKind.String), tokens);
        Assert.Contains(("$HOME", SyntaxKind.Variable), tokens);
        Assert.Contains(("# done", SyntaxKind.Comment), tokens);
    }

    [Fact]
    public void Colours_diff_lines()
    {
        var tokens = SyntaxHighlighter.Highlight("-old\n+new", "diff");

        Assert.Equal([("-old\n", SyntaxKind.Removed), ("+new", SyntaxKind.Added)], tokens);
    }

    [Theory]
    [InlineData(@"\alpha + \beta^2", "α + β²")]
    [InlineData(@"\frac{1}{x+1}", "1/(x+1)")]
    [InlineData(@"x_{i} \in \mathbb{R}", "xᵢ ∈ ℝ")]
    [InlineData(@"\sqrt[3]{y}", "³√y")]
    [InlineData(@"e^{i\pi}", "e^(iπ)")]
    public void Writes_maths_as_unicode(string tex, string expected) =>
        Assert.Equal(expected, MathText.ToUnicode(tex));
}
