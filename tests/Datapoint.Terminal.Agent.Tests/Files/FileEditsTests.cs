using Datapoint.Terminal.Agent.Files;

namespace Datapoint.Terminal.Agent.Tests.Files;

public sealed class FileEditsTests
{
    [Fact]
    public void Applies_edits_in_order()
    {
        var text = FileEdits.Apply("listen 80;\nserver_name a;\n", [new("listen 80;", "listen 443 ssl;"), new("server_name a;", "server_name b;")], out var problem);

        Assert.Null(problem);
        Assert.Equal("listen 443 ssl;\nserver_name b;\n", text);
    }

    [Fact]
    public void Keeps_windows_line_endings()
    {
        var text = FileEdits.Apply("a\r\nb\r\nc\r\n", [new("a\nb", "a\nB")], out var problem);

        Assert.Null(problem);
        Assert.Equal("a\r\nB\r\nc\r\n", text);
    }

    [Theory]
    [InlineData("x", "isn't in the file")]
    [InlineData("a", "appears more than once")]
    [InlineData("", "is empty")]
    public void Refuses_text_that_is_not_found_exactly_once(string old, string expected)
    {
        var text = FileEdits.Apply("a\na\n", [new(old, "b")], out var problem);

        Assert.Contains(expected, problem, StringComparison.Ordinal);
        Assert.Equal("a\na\n", text);
    }

    [Fact]
    public void Names_the_edit_that_failed()
    {
        FileEdits.Apply("one\ntwo\n", [new("one", "1"), new("three", "3")], out var problem);

        Assert.StartsWith("Edit 2's text to replace isn't in the file.", problem, StringComparison.Ordinal);
    }
}
