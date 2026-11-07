using Datapoint.Terminal.Agent.Files;

namespace Datapoint.Terminal.Agent.Tests.Files;

public sealed class TextDiffTests
{
    private static string Numbered(int count, Func<int, string?>? change = null) =>
        string.Concat(Enumerable.Range(1, count).Select(line => (change?.Invoke(line) ?? $"line {line}") + "\n"));

    [Fact]
    public void Shows_a_changed_line_with_three_lines_of_context()
    {
        var diff = TextDiff.Unified("/etc/app.conf", Numbered(10), Numbered(10, line => line == 5 ? "line five" : null));

        Assert.Equal(
            "--- /etc/app.conf\n+++ /etc/app.conf\n@@ -2,7 +2,7 @@\n line 2\n line 3\n line 4\n-line 5\n+line five\n line 6\n line 7\n line 8\n",
            diff);
    }

    [Fact]
    public void Keeps_distant_changes_in_separate_hunks()
    {
        var diff = TextDiff.Unified("f", Numbered(30), Numbered(30, line => line is 2 or 25 ? "changed" : null));

        Assert.Contains("@@ -1,5 +1,5 @@\n", diff, StringComparison.Ordinal);
        Assert.Contains("@@ -22,7 +22,7 @@\n", diff, StringComparison.Ordinal);
    }

    [Fact]
    public void Shows_added_and_removed_lines()
    {
        var diff = TextDiff.Unified("f", "a\nb\nc\n", "a\nc\nd\n");

        Assert.Equal("--- f\n+++ f\n@@ -1,3 +1,3 @@\n a\n-b\n c\n+d\n", diff);
    }

    [Fact]
    public void Shows_a_new_file_as_all_added()
    {
        Assert.Equal("--- f\n+++ f\n@@ -0,0 +1,2 @@\n+one\n+two\n", TextDiff.Unified("f", string.Empty, "one\ntwo\n"));
    }

    [Fact]
    public void Shows_nothing_for_identical_texts()
    {
        Assert.Equal("--- f\n+++ f\n", TextDiff.Unified("f", "same\n", "same\n"));
    }
}
