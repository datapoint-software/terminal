using System.Text;
using Datapoint.Terminal.Agent.Workspaces;

namespace Datapoint.Terminal.Agent.Tests.Workspaces;

public sealed class InstructionFilesTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("datapoint-instructions-").FullName;

    private string Home => Path.Combine(_root, "home");

    private string Global => Path.Combine(Home, ".datapoint-terminal");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static string Write(string folder, string name, string text = "x")
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, name);
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void Reads_only_the_workspace_s_own_terminal_md()
    {
        var workspace = Path.Combine(Home, "work", "site");
        Write(Home, "TERMINAL.md");
        Write(Path.Combine(Home, "work"), "TERMINAL.md");
        Write(workspace, "AGENTS.md");
        var own = Write(workspace, "TERMINAL.md");

        Assert.Equal([own], InstructionFiles.FindForWorkspace(workspace, globalFile: null));
    }

    [Fact]
    public void Agents_md_is_not_read()
    {
        var workspace = Path.Combine(Home, "work");
        Write(workspace, "AGENTS.md");

        Assert.Empty(InstructionFiles.FindForWorkspace(workspace, globalFile: null));
    }

    [Fact]
    public void Finds_the_global_file()
    {
        var global = Write(Global, "TERMINAL.md");

        Assert.Equal(global, InstructionFiles.FindGlobal(Global));
    }

    [Fact]
    public void Cuts_files_at_the_shared_limit_without_splitting_a_character()
    {
        var first = Write(Home, "a.md", "abcdef");
        var second = Write(Home, "b.md", "ééé");
        var third = Write(Home, "c.md", "more");

        var files = InstructionFiles.Read([first, second, third], maxBytes: 9);

        Assert.Equal(("abcdef", false), (files[0].Text, files[0].IsCut));
        Assert.Equal(("é", true), (files[1].Text, files[1].IsCut));
        Assert.Equal(("m", true), (files[2].Text, files[2].IsCut));
        Assert.True(Encoding.UTF8.GetByteCount(string.Concat(files.Select(file => file.Text))) <= 9);
    }

    [Fact]
    public void Drops_a_byte_order_mark()
    {
        var path = Path.Combine(Home, "TERMINAL.md");
        Directory.CreateDirectory(Home);
        File.WriteAllBytes(path, [0xEF, 0xBB, 0xBF, (byte)'h', (byte)'i']);

        Assert.Equal("hi", InstructionFiles.Read([path])[0].Text);
    }
}
