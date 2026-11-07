using Datapoint.Terminal.Agent.Files;
using Datapoint.Terminal.Agent.Hosts;

namespace Datapoint.Terminal.Agent.Tests.Hosts;

public sealed class HostDocumentTests
{
    [Fact]
    public void Starts_from_the_standard_sections()
    {
        var (text, result) = HostDocument.SetSection(string.Empty, "web1", "System", "Debian 12 on 4 vCPUs.");

        Assert.Equal(WriteResult.Changed, result);
        Assert.StartsWith("# web1\n", text, StringComparison.Ordinal);
        Assert.Equal(HostDocument.StandardSections, HostDocument.Sections(text), StringComparer.Ordinal);
        Assert.Equal("Debian 12 on 4 vCPUs.", HostDocument.Section(text, "system"));
    }

    [Fact]
    public void Replaces_only_its_section()
    {
        const string Before = "# web1\n\nMy intro.\n\n## System\n\nOld.\n\n## Notes\n\nThe user's note.\n";

        var (text, _) = HostDocument.SetSection(Before, "web1", "System", "New.");

        Assert.Equal("# web1\n\nMy intro.\n\n## System\n\nNew.\n\n## Notes\n\nThe user's note.\n", text);
    }

    [Fact]
    public void Adds_a_new_section_at_the_end()
    {
        var start = HostDocument.Template("web1");

        var (text, _) = HostDocument.SetSection(start, "web1", "Backups", "Nightly to S3.");

        Assert.Equal(["Overview", "System", "Software and services", "Notes", "Backups"], HostDocument.Sections(text), StringComparer.Ordinal);
    }

    [Fact]
    public void Reports_no_change_when_the_section_is_the_same()
    {
        var (text, _) = HostDocument.SetSection(string.Empty, "web1", "System", "Debian 12.");

        Assert.Equal(WriteResult.Unchanged, HostDocument.SetSection(text, "web1", "System", "Debian 12.").Result);
    }

    [Fact]
    public void Splits_a_legacy_log_into_entries()
    {
        const string Before = "# web1\n\n## Notes\n\nMine.\n\n## Maintenance log\n\n- 2026-01-01: Set up.\n- Patched, undated.\n\n## Extra\n\nThe user's section.\n";

        var (text, log) = HostDocument.SplitLegacyLog(Before);

        Assert.Equal("# web1\n\n## Notes\n\nMine.\n\n## Extra\n\nThe user's section.\n", text);
        Assert.Equal([(new DateOnly(2026, 1, 1), "Set up."), (DateOnly.MinValue, "Patched, undated.")], log);
    }

    [Fact]
    public void Refuses_to_grow_past_the_limit()
    {
        var (_, result) = HostDocument.SetSection(string.Empty, "web1", "Notes", new string('x', HostDocument.MaxBytes));

        Assert.Equal(WriteResult.Full, result);
    }

    [Theory]
    [InlineData("web-1", "web-1")]
    [InlineData("db primary", "db-primary")]
    [InlineData("../../etc/passwd", "etc-passwd")]
    [InlineData("..", "host")]
    public void Names_the_folder_inside_hosts(string connection, string expected)
    {
        Assert.Equal(expected, HostDocument.FolderNameFor(connection));
    }
}
