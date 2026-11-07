using System.Text;
using Datapoint.Terminal.Agent.Projects;

namespace Datapoint.Terminal.Agent.Tests.Projects;

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
    public void Reads_from_the_home_folder_down_to_the_project()
    {
        var project = Path.Combine(Home, "work", "site");
        var home = Write(Home, "AGENTS.md");
        var work = Write(Path.Combine(Home, "work"), "TERMINAL.md");
        var site = Write(project, "AGENTS.md");
        Write(_root, "TERMINAL.md");

        Assert.Equal([home, work, site], InstructionFiles.FindForProject(project, Home, globalFile: null));
    }

    [Fact]
    public void Prefers_terminal_md_over_agents_md_in_the_same_folder()
    {
        var project = Path.Combine(Home, "work");
        Write(project, "AGENTS.md");
        var terminal = Write(project, "TERMINAL.md");

        Assert.Equal([terminal], InstructionFiles.FindForProject(project, Home, globalFile: null));
    }

    [Fact]
    public void A_project_outside_the_home_folder_reads_only_its_own_folder()
    {
        var project = Path.Combine(_root, "shared", "project");
        Write(Path.Combine(_root, "shared"), "AGENTS.md");
        var own = Write(project, "AGENTS.md");

        Assert.Equal([own], InstructionFiles.FindForProject(project, Home, globalFile: null));
    }

    [Fact]
    public void The_global_file_is_not_read_twice_for_the_default_project()
    {
        var global = Write(Global, "TERMINAL.md");
        var project = Path.Combine(Global, "default");
        Directory.CreateDirectory(project);

        Assert.Equal(global, InstructionFiles.FindGlobal(Global));
        Assert.Empty(InstructionFiles.FindForProject(project, Home, global));
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
