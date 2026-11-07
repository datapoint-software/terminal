using Datapoint.Terminal.Agent.Skills;

namespace Datapoint.Terminal.Agent.Tests.Skills;

public sealed class SkillDocumentTests : IDisposable
{
    private const string Patch = """
        ---
        name: patch-and-reboot
        description: "Installs updates and reboots: one web server at a time."
        license: Apache-2.0
        metadata:
          author: ops
          terminal.labels: "web, debian"
          terminal.rollout: batches
          terminal.batch-size: "2"
          terminal.health-check: "curl -fsS http://localhost/health"
          terminal.healthy-for: 5m
          terminal.timeout: "1h"
          terminal.on-failure: ask
        ---

        # Patch and reboot

        1. `sudo apt-get upgrade -y`
        """;

    private readonly string _folder = Directory.CreateTempSubdirectory("skills-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void Reads_where_a_skill_runs_and_how_it_rolls_out_from_its_metadata()
    {
        var skill = SkillDocument.Read(Patch, "/w/skills/patch-and-reboot/SKILL.md");

        Assert.Equal(("patch-and-reboot", "Patch and reboot", "Installs updates and reboots: one web server at a time."), (skill.Name, skill.Title, skill.Description));
        Assert.Equal(["web", "debian"], skill.Labels);
        Assert.Equal(
            new SkillRollout { Order = RolloutOrder.Batches, BatchSize = 2, HealthCheck = "curl -fsS http://localhost/health", HealthyFor = TimeSpan.FromMinutes(5), Timeout = TimeSpan.FromHours(1), OnFailure = RolloutFailure.Ask },
            skill.Rollout);
        Assert.StartsWith("# Patch and reboot", skill.Body, StringComparison.Ordinal);
        Assert.True(skill.RunsOn(["Web", "debian", "eu"]));
        Assert.False(skill.RunsOn(["web"]));
        Assert.False(skill.RunsOn(null));
        Assert.True((skill with { Labels = [] }).RunsOn(null));
    }

    [Fact]
    public void Rewrites_only_its_own_metadata_keeping_other_fields()
    {
        var text = SkillDocument.WithSettings(Patch, ["db"], new SkillRollout { OnFailure = RolloutFailure.Continue });
        var skill = SkillDocument.Read(text, "/w/skills/patch-and-reboot/SKILL.md");
        var header = SkillDocument.ReadHeader(text);

        Assert.Equal(["db"], skill.Labels);
        Assert.Equal((RolloutOrder.OneByOne, null, RolloutFailure.Continue), (skill.Rollout.Order, skill.Rollout.HealthCheck, skill.Rollout.OnFailure));
        Assert.Equal("Apache-2.0", header.Field("license"));
        Assert.Equal("ops", header.MetadataValue("author"));
        Assert.Contains("description: \"Installs updates and reboots: one web server at a time.\"", text, StringComparison.Ordinal);
        Assert.EndsWith("1. `sudo apt-get upgrade -y`", text.TrimEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public void Creates_a_valid_agent_skills_file_that_reads_back()
    {
        var rollout = new SkillRollout { HealthCheck = "systemctl is-active \"nginx\"", HealthyFor = TimeSpan.FromSeconds(90) };

        var text = SkillDocument.Create("renew", "Renews: certificates # all", ["web"], rollout, "# Renew\n\nSteps.");
        var skill = SkillDocument.Read(text, "/w/skills/renew/SKILL.md");

        Assert.StartsWith("---\nname: renew\n", text, StringComparison.Ordinal);
        Assert.Equal("Renews: certificates # all", skill.Description);
        Assert.Equal(rollout, skill.Rollout);
    }

    [Theory]
    [InlineData("Patch and reboot", "patch-and-reboot")]
    [InlineData("  Renew TLS -- certs! ", "renew-tls-certs")]
    [InlineData("???", "skill")]
    public void Names_a_skill_as_the_format_requires(string title, string expected)
    {
        Assert.Equal(expected, SkillDocument.NameFor(title));
        Assert.True(SkillDocument.IsValidName(expected));
    }

    [Theory]
    [InlineData("90s", 90)]
    [InlineData("5m", 300)]
    [InlineData("2", 120)]
    [InlineData("1.5h", 5400)]
    public void Reads_durations(string text, int seconds) =>
        Assert.Equal(TimeSpan.FromSeconds(seconds), SkillDocument.Duration(text));

    [Theory]
    [InlineData("forever")]
    [InlineData("25h")]
    [InlineData("-1m")]
    public void Refuses_durations_it_cant_read(string text) => Assert.Null(SkillDocument.Duration(text));

    [Fact]
    public void Starts_as_many_hosts_together_as_the_order_says()
    {
        Assert.Equal(1, new SkillRollout().Width(8));
        Assert.Equal(3, new SkillRollout { Order = RolloutOrder.Batches, BatchSize = 3 }.Width(8));
        Assert.Equal(5, new SkillRollout { Order = RolloutOrder.AllAtOnce, MaxHosts = 5 }.Width(8));
        Assert.Equal(2, new SkillRollout { Order = RolloutOrder.AllAtOnce }.Width(2));
    }

    [Fact]
    public void Marks_a_rollout_made_less_careful()
    {
        var careful = new SkillRollout { HealthCheck = "true", HealthyFor = TimeSpan.FromMinutes(5) };

        Assert.True((careful with { Order = RolloutOrder.AllAtOnce }).IsLooserThan(careful));
        Assert.True((careful with { HealthCheck = null }).IsLooserThan(careful));
        Assert.True((careful with { HealthyFor = TimeSpan.FromMinutes(1) }).IsLooserThan(careful));
        Assert.True((careful with { OnFailure = RolloutFailure.Continue }).IsLooserThan(careful));
        Assert.False((careful with { Timeout = TimeSpan.FromMinutes(5) }).IsLooserThan(careful));
    }

    [Fact]
    public void Turns_runbooks_into_skills_once()
    {
        Directory.CreateDirectory(Path.Combine(_folder, "runbooks"));
        File.WriteAllText(Path.Combine(_folder, "runbooks", "renew-certificates.md"), "# Renew certificates\n\nRenews a certificate.\n\n## Steps\n\n1. Renew.\n");

        SkillsLayout.Upgrade(_folder);

        var skill = Assert.Single(new SkillFolder(Path.Combine(_folder, "skills"), _ => false, CanWrite: true, () => true).List());
        Assert.Equal(("renew-certificates", "Renew certificates", "Renews a certificate."), (skill.Name, skill.Title, skill.Description));
        Assert.False(Directory.Exists(Path.Combine(_folder, "runbooks")));
    }
}
