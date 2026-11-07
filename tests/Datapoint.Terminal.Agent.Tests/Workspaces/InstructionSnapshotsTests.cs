using Datapoint.Terminal.Agent.Workspaces;
using Datapoint.Terminal.Agent.Safety;

namespace Datapoint.Terminal.Agent.Tests.Workspaces;

public sealed class InstructionSnapshotsTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("datapoint-snapshots-").FullName;
    private readonly SecretRedactor _redactor = new();

    private string StorePath => Path.Combine(_folder, "data", "instructions-read.json");

    private string FilePath => Path.Combine(_folder, "AGENTS.md");

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    // The file exists on disk, since snapshots of files that no longer exist are dropped.
    private InstructionFile Read(string text)
    {
        System.IO.File.WriteAllText(FilePath, text);
        return new InstructionFile(FilePath, text, IsCut: false);
    }

    private IReadOnlyList<InstructionChange> Update(InstructionFile file) => new InstructionSnapshots(StorePath).Update([file], _redactor);

    [Fact]
    public void Reports_a_file_that_changed_since_it_was_last_read_even_after_a_restart()
    {
        Assert.Empty(Update(Read("Use doas.")));

        var changes = Update(Read("Use sudo."));

        Assert.Equal(new InstructionChange(FilePath, "Use doas.", "Use sudo."), Assert.Single(changes));
    }

    [Fact]
    public void Reports_each_change_once()
    {
        var snapshots = new InstructionSnapshots(StorePath);
        snapshots.Update([Read("a")], _redactor);
        snapshots.Update([Read("b")], _redactor);

        Assert.Empty(snapshots.Update([Read("b")], _redactor));
    }

    [Fact]
    public void Skips_files_that_could_not_be_read()
    {
        var snapshots = new InstructionSnapshots(StorePath);
        snapshots.Update([Read("a")], _redactor);

        Assert.Empty(snapshots.Update([new InstructionFile(FilePath, string.Empty, IsCut: false, "Permission denied")], _redactor));
        Assert.Empty(snapshots.Update([Read("a")], _redactor));
    }

    [Fact]
    public void Stores_only_masked_text()
    {
        Update(Read("Deploy with DB_PASSWORD=hunter2hunter2"));

        var stored = System.IO.File.ReadAllText(StorePath);
        Assert.DoesNotContain("hunter2", stored, StringComparison.Ordinal);
        Assert.Contains(SecretRedactor.Mask, stored, StringComparison.Ordinal);
    }

    [Fact]
    public void Notices_a_change_only_to_a_secret()
    {
        Update(Read("DB_PASSWORD=hunter2hunter2"));

        var change = Assert.Single(Update(Read("DB_PASSWORD=swordfish-swordfish")));

        Assert.True(change.IsOnlySecrets);
        Assert.DoesNotContain("swordfish", change.After, StringComparison.Ordinal);
    }

    [Fact]
    public void Masks_what_earlier_versions_stored_without_reporting_a_change()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
        var key = System.Text.Json.JsonEncodedText.Encode(FilePath);
        System.IO.File.WriteAllText(StorePath, "{\"" + key + "\": \"token=hunter2hunter2\"}");

        Assert.Empty(Update(Read("token=hunter2hunter2")));
        Assert.DoesNotContain("hunter2", System.IO.File.ReadAllText(StorePath), StringComparison.Ordinal);
    }

    [Fact]
    public void Drops_files_that_no_longer_exist()
    {
        Update(Read("Use doas."));
        System.IO.File.Delete(FilePath);
        var other = Path.Combine(_folder, "TERMINAL.md");
        System.IO.File.WriteAllText(other, "x");

        new InstructionSnapshots(StorePath).Update([new InstructionFile(other, "x", IsCut: false)], _redactor);

        Assert.DoesNotContain("doas", System.IO.File.ReadAllText(StorePath), StringComparison.Ordinal);
    }

    [Fact]
    public void Checks_a_file_against_the_accepted_copy_without_accepting_the_change()
    {
        Read("Use doas.");
        var store = new InstructionSnapshots(StorePath);
        Assert.Equal(ReviewStatus.New, store.Check(FilePath, "Use doas.", _redactor, out _));

        store.Accept(FilePath, "Use doas.", _redactor);
        Read("Use sudo.");

        var later = new InstructionSnapshots(StorePath);
        Assert.Equal(ReviewStatus.Changed, later.Check(FilePath, "Use sudo.", _redactor, out var accepted));
        Assert.Equal("Use doas.", accepted!.Text);
        Assert.Equal(ReviewStatus.Changed, later.Check(FilePath, "Use sudo.", _redactor, out _));
        Assert.Equal(ReviewStatus.Accepted, later.Check(FilePath, "Use doas.", _redactor, out _));
    }

    [Fact]
    public void Knows_a_folder_once_it_is_accepted()
    {
        var store = new InstructionSnapshots(StorePath);
        Assert.False(store.IsKnown(_folder, _redactor));

        store.Accept(_folder, string.Empty, _redactor);

        Assert.True(new InstructionSnapshots(StorePath).IsKnown(_folder, _redactor));
    }

    [Fact]
    public void Tells_whether_the_accepted_copy_can_be_written_back()
    {
        Read("x");
        var store = new InstructionSnapshots(StorePath);
        store.Accept(FilePath, "Use doas.", _redactor);
        store.Check(FilePath, "x", _redactor, out var plain);
        store.Accept(FilePath, "token ghp_abcdefghijklmnopqrstuvwxyz0123456789", _redactor);
        store.Check(FilePath, "x", _redactor, out var masked);

        Assert.True(InstructionSnapshots.IsWhole(plain!));
        Assert.False(InstructionSnapshots.IsWhole(masked!));
    }
}
