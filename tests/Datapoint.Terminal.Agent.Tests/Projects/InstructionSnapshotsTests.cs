using Datapoint.Terminal.Agent.Projects;

namespace Datapoint.Terminal.Agent.Tests.Projects;

public sealed class InstructionSnapshotsTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("datapoint-snapshots-").FullName;

    private string StorePath => Path.Combine(_folder, "data", "instructions-read.json");

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static InstructionFile File(string text) => new("/work/AGENTS.md", text, IsCut: false);

    [Fact]
    public void Reports_a_file_that_changed_since_it_was_last_read_even_after_a_restart()
    {
        Assert.Empty(new InstructionSnapshots(StorePath).Update([File("Use doas.")]));

        var changes = new InstructionSnapshots(StorePath).Update([File("Use sudo.")]);

        Assert.Equal(new InstructionChange("/work/AGENTS.md", "Use doas.", "Use sudo."), Assert.Single(changes));
    }

    [Fact]
    public void Reports_each_change_once()
    {
        var snapshots = new InstructionSnapshots(StorePath);
        snapshots.Update([File("a")]);
        snapshots.Update([File("b")]);

        Assert.Empty(snapshots.Update([File("b")]));
    }

    [Fact]
    public void Skips_files_that_could_not_be_read()
    {
        var snapshots = new InstructionSnapshots(StorePath);
        snapshots.Update([File("a")]);

        Assert.Empty(snapshots.Update([new InstructionFile("/work/AGENTS.md", string.Empty, IsCut: false, "Permission denied")]));
        Assert.Empty(snapshots.Update([File("a")]));
    }
}
