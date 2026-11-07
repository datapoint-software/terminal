using Datapoint.Terminal.Agent.Hosts;

namespace Datapoint.Terminal.Agent.Tests.Hosts;

public sealed class HostLogTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("host-log-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void Writes_one_file_per_entry_and_reads_them_newest_first()
    {
        var earlier = new DateTimeOffset(2026, 11, 3, 9, 10, 0, TimeSpan.Zero);
        var later = new DateTimeOffset(2026, 11, 7, 14, 32, 0, TimeSpan.Zero);
        HostLog.Write(_folder, earlier, "rita", withAgent: false, "Renewed the certificate.");
        HostLog.Write(_folder, later, "joao", withAgent: true, "Patched and rebooted.");

        var entries = HostLog.Read(_folder);

        Assert.Equal(2, Directory.GetFiles(_folder, "*.md").Length);
        Assert.Equal(["Patched and rebooted.", "Renewed the certificate."], entries.Select(entry => entry.Text).ToList());
        Assert.Equal((later, "joao", true), (entries[0].When, entries[0].Author, entries[0].WithAgent));
    }

    [Fact]
    public void Two_entries_at_the_same_moment_get_their_own_files()
    {
        var when = new DateTimeOffset(2026, 11, 7, 14, 32, 0, TimeSpan.Zero);

        var first = HostLog.Write(_folder, when, "joao", withAgent: true, "One.");
        var second = HostLog.Write(_folder, when, "rita", withAgent: true, "Two.");

        Assert.False(string.Equals(first, second, StringComparison.Ordinal));
        Assert.Equal(2, HostLog.Read(_folder).Count);
    }

    [Fact]
    public void Reads_an_entry_without_a_header()
    {
        var entry = HostLog.Parse(Path.Combine(_folder, "x.md"), "Rebooted by hand.\n");

        Assert.Equal(("Rebooted by hand.", string.Empty), (entry!.Text, entry.Author));
    }

    [Fact]
    public void Describes_entries_for_the_agent()
    {
        HostLog.Write(_folder, new DateTimeOffset(2026, 11, 7, 14, 32, 0, TimeSpan.Zero), "joao", withAgent: true, "Patched.");

        Assert.Equal("## Maintenance log\n\n- 2026-11-07 14:32 (joao, with the agent): Patched.\n", HostLog.Describe(HostLog.Read(_folder)));
    }

    [Fact]
    public void Upgrades_a_single_file_document_into_a_folder()
    {
        File.WriteAllText(Path.Combine(_folder, "web1.md"), "# web1\n\n## Notes\n\nMine.\n\n## Maintenance log\n\n- 2026-01-01: Set up.\n");

        HostDocsLayout.Upgrade(_folder, author: string.Empty);

        Assert.False(File.Exists(Path.Combine(_folder, "web1.md")));
        Assert.Equal("# web1\n\n## Notes\n\nMine.\n", File.ReadAllText(Path.Combine(_folder, "web1", "README.md")));
        Assert.Equal("Set up.", Assert.Single(HostLog.Read(Path.Combine(_folder, "web1", "log"))).Text);
    }
}
