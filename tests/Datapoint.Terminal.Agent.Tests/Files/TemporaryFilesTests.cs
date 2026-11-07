using Datapoint.Terminal.Agent.Files;

namespace Datapoint.Terminal.Agent.Tests.Files;

public sealed class TemporaryFilesTests : IDisposable
{
    private readonly string _workspace = Directory.CreateTempSubdirectory("workspace-").FullName;
    private readonly string _outside = Directory.CreateTempSubdirectory("outside-").FullName;

    public void Dispose()
    {
        Directory.Delete(_workspace, recursive: true);
        Directory.Delete(_outside, recursive: true);
    }

    [Fact]
    public void Keeps_the_temporary_file_outside_the_workspace_on_the_same_disk()
    {
        var temporary = TemporaryFiles.For(Path.Combine(_workspace, "SKILL.md"), _outside);

        Assert.Equal(Path.GetFullPath(_outside), Path.GetDirectoryName(temporary));
        Assert.StartsWith("SKILL.md.", Path.GetFileName(temporary), StringComparison.Ordinal);
    }

    [Fact]
    public void Falls_back_to_a_name_sync_services_skip_on_another_disk()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "/dev is a separate mount on macOS and Linux only.");

        var temporary = TemporaryFiles.For(Path.Combine(_workspace, "SKILL.md"), "/dev/datapoint-terminal-test");

        Assert.Equal(Path.GetFullPath(_workspace), Path.GetDirectoryName(temporary));
        Assert.StartsWith("~$SKILL.md.", Path.GetFileName(temporary), StringComparison.Ordinal);
    }

    [Fact]
    public void Leaves_nothing_behind_in_the_workspace_after_a_write()
    {
        var path = Path.Combine(_workspace, "skills", "patch", "SKILL.md");

        TextFiles.Write(path, "# Patch\n", isPrivate: false);

        Assert.Equal("# Patch\n", File.ReadAllText(path));
        Assert.Equal("SKILL.md", Path.GetFileName(Assert.Single(Directory.GetFiles(Path.GetDirectoryName(path)!))));
    }
}
