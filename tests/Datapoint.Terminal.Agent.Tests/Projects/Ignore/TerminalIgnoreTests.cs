using Datapoint.Terminal.Agent.Projects.Ignore;

namespace Datapoint.Terminal.Agent.Tests.Projects.Ignore;

public sealed class TerminalIgnoreTests : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("datapoint-ignore-").FullName;

    public void Dispose() => Directory.Delete(_home, recursive: true);

    private string Project => Path.Combine(_home, "code", "api");

    private string File(string relative) => Path.Combine([Project, .. relative.Split('/')]);

    private static void Write(string folder, params string[] lines)
    {
        Directory.CreateDirectory(folder);
        System.IO.File.WriteAllLines(Path.Combine(folder, TerminalIgnore.FileName), lines);
    }

    private bool IsIgnored(string relative) => TerminalIgnore.IsIgnored(File(relative), Project, _home);

    [Fact]
    public void Sends_everything_without_ignore_files()
    {
        Directory.CreateDirectory(Project);

        Assert.False(IsIgnored("AGENTS.md"));
    }

    [Fact]
    public void Reads_the_project_folders_and_those_above_up_to_home()
    {
        Write(_home, "*.secret");
        Write(Path.Combine(_home, "code"), "TERMINAL.md");
        Write(Path.Combine(Project, "docs"), "private/");

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
        Write(Project, "!TERMINAL.md");

        Assert.False(IsIgnored("TERMINAL.md"));
    }

    [Fact]
    public void Reads_only_the_project_down_when_it_is_outside_home()
    {
        var home = Path.Combine(_home, "home");
        var shared = Path.Combine(_home, "shared");
        var project = Path.Combine(shared, "project");
        Directory.CreateDirectory(home);
        Write(shared, "*.md");
        Write(project, "TERMINAL.md");

        Assert.False(TerminalIgnore.IsIgnored(Path.Combine(project, "AGENTS.md"), project, home));
        Assert.True(TerminalIgnore.IsIgnored(Path.Combine(project, "TERMINAL.md"), project, home));
    }
}
