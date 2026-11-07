using Datapoint.Terminal.Agent.Workspaces.Ignore;

namespace Datapoint.Terminal.Agent.Tests.Workspaces.Ignore;

public sealed class TerminalIgnoreTests : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("datapoint-ignore-").FullName;

    public void Dispose() => Directory.Delete(_home, recursive: true);

    private string Workspace => Path.Combine(_home, "code", "api");

    private string File(string relative) => Path.Combine([Workspace, .. relative.Split('/')]);

    private static void Write(string folder, params string[] lines)
    {
        Directory.CreateDirectory(folder);
        System.IO.File.WriteAllLines(Path.Combine(folder, TerminalIgnore.FileName), lines);
    }

    private bool IsIgnored(string relative) => TerminalIgnore.IsIgnored(File(relative), Workspace, _home);

    [Fact]
    public void Sends_everything_without_ignore_files()
    {
        Directory.CreateDirectory(Workspace);

        Assert.False(IsIgnored("AGENTS.md"));
    }

    [Fact]
    public void Reads_the_workspace_folders_and_those_above_up_to_home()
    {
        Write(_home, "*.secret");
        Write(Path.Combine(_home, "code"), "TERMINAL.md");
        Write(Path.Combine(Workspace, "docs"), "private/");

        Assert.True(IsIgnored("keys.secret"));
        Assert.True(IsIgnored("TERMINAL.md"));
        Assert.True(IsIgnored("docs/private/AGENTS.md"));
        Assert.False(IsIgnored("AGENTS.md"));
        Assert.False(IsIgnored("private/AGENTS.md"));
    }

    [Fact]
    public void Nearer_files_can_bring_a_file_back()
    {
        Write(_home, "TERMINAL.md");
        Write(Workspace, "!TERMINAL.md");

        Assert.False(IsIgnored("TERMINAL.md"));
    }

    [Fact]
    public void Reads_only_the_workspace_down_when_it_is_outside_home()
    {
        var home = Path.Combine(_home, "home");
        var shared = Path.Combine(_home, "shared");
        var workspace = Path.Combine(shared, "workspace");
        Directory.CreateDirectory(home);
        Write(shared, "*.md");
        Write(workspace, "TERMINAL.md");

        Assert.False(TerminalIgnore.IsIgnored(Path.Combine(workspace, "AGENTS.md"), workspace, home));
        Assert.True(TerminalIgnore.IsIgnored(Path.Combine(workspace, "TERMINAL.md"), workspace, home));
    }

    [Fact]
    public void Describes_the_ignore_files_above_and_inside_the_workspace()
    {
        Write(_home, "*.secret");
        Write(Workspace, "# keys", ".env", "");
        Write(Path.Combine(Workspace, "deploy"), "*.pem");
        Write(Path.Combine(Workspace, ".hidden"), "skipped");
        Write(Path.Combine(_home, "elsewhere"), "unrelated");

        var described = TerminalIgnore.Describe(Workspace, _home);

        Assert.Equal(
            [(_home, "*.secret"), (Workspace, ".env"), (Path.Combine(Workspace, "deploy"), "*.pem")],
            described.Select(file => (file.Folder, string.Join(',', file.Patterns))));
    }
}
