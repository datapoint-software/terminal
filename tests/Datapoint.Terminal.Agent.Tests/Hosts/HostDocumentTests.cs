using Datapoint.Terminal.Agent.Files;
using Datapoint.Terminal.Agent.Hosts;

namespace Datapoint.Terminal.Agent.Tests.Hosts;

public sealed class HostDocumentTests
{
    private static readonly DateOnly s_date = new(2026, 11, 7);

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
    public void Adds_a_new_section_before_the_log()
    {
        var start = HostDocument.Template("web1");

        var (text, _) = HostDocument.SetSection(start, "web1", "Backups", "Nightly to S3.");

        Assert.Equal(["Overview", "System", "Software and services", "Notes", "Backups", "Maintenance log"], HostDocument.Sections(text), StringComparer.Ordinal);
    }

    [Fact]
    public void Reports_no_change_when_the_section_is_the_same()
    {
        var (text, _) = HostDocument.SetSection(string.Empty, "web1", "System", "Debian 12.");

        Assert.Equal(WriteResult.Unchanged, HostDocument.SetSection(text, "web1", "System", "Debian 12.").Result);
    }

    [Fact]
    public void Appends_dated_entries_to_the_log_in_order()
    {
        var (first, _) = HostDocument.AddLogEntry(HostDocument.Template("web1"), "web1", s_date, "Installed nginx.");
        var (second, _) = HostDocument.AddLogEntry(first, "web1", s_date.AddDays(1), "Renewed the certificate.");

        Assert.Equal("- 2026-11-07: Installed nginx.\n- 2026-11-08: Renewed the certificate.", HostDocument.Section(second, HostDocument.LogSection));
    }

    [Fact]
    public void Keeps_the_log_entry_out_of_a_following_section()
    {
        const string Before = "# web1\n\n## Maintenance log\n\n- 2026-01-01: Set up.\n\n## Extra\n\nThe user's section.\n";

        var (text, _) = HostDocument.AddLogEntry(Before, "web1", s_date, "Patched.");

        Assert.Equal("# web1\n\n## Maintenance log\n\n- 2026-01-01: Set up.\n- 2026-11-07: Patched.\n\n## Extra\n\nThe user's section.\n", text);
    }

    [Fact]
    public void Refuses_to_grow_past_the_limit()
    {
        var (_, result) = HostDocument.SetSection(string.Empty, "web1", "Notes", new string('x', HostDocument.MaxBytes));

        Assert.Equal(WriteResult.Full, result);
    }

    [Theory]
    [InlineData("web-1", "web-1.md")]
    [InlineData("db primary", "db-primary.md")]
    [InlineData("../../etc/passwd", "etc-passwd.md")]
    [InlineData("..", "host.md")]
    public void Names_the_file_inside_the_folder(string connection, string expected)
    {
        Assert.Equal(expected, HostDocument.FileNameFor(connection));
    }
}
