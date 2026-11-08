using Datapoint.Terminal.Agent.Safety;

namespace Datapoint.Terminal.Agent.Tests.Safety;

public sealed class LearnedCommandsTests
{
    [Theory]
    [InlineData("docker swarm ca", "docker swarm ca")]
    [InlineData("docker swarm ca --quiet", "docker swarm ca")]
    [InlineData("nvidia-smi", "nvidia-smi")]
    [InlineData("nvidia-smi -q", "nvidia-smi")]
    [InlineData("patronictl list --format json", "patronictl list")]
    [InlineData("patronictl -c /etc/patroni.yml list", null)]
    [InlineData("vault status", "vault status")]
    [InlineData("TZ=UTC vault status", "vault status")]
    [InlineData("LD_PRELOAD=x.so vault status", null)]
    [InlineData("systemctl restart nginx", null)]
    [InlineData("kubectl delete pod web", null)]
    [InlineData("kubectl exec web -- ls", null)]
    [InlineData("sudo vault status", "vault status")]
    [InlineData("sudo -E vault status", null)]
    [InlineData("bash -c 'vault status'", null)]
    [InlineData("./vault status", null)]
    [InlineData("vault status | head", null)]
    [InlineData("vault status > out", null)]
    [InlineData("vault status -o out", null)]
    [InlineData("curl https://example.org", null)]
    [InlineData("ls -la", null)]
    [InlineData("ssh-keygen -l -f key", null)]
    [InlineData("rm -rf build", null)]
    public void Suggests_the_program_and_subcommands_a_command_starts_with(string command, string? expected) =>
        Assert.Equal(expected, LearnedCommands.Suggest(command));

    [Theory]
    [InlineData("  docker   swarm  ca ", "docker swarm ca")]
    [InlineData("dpkg-query -W", "dpkg-query -W")]
    [InlineData("a b c d", null)]
    [InlineData("/usr/bin/vault status", null)]
    [InlineData("vault status;reboot", null)]
    [InlineData("python3", null)]
    [InlineData("docker run", null)]
    [InlineData("DOCKER RUN", null)]
    [InlineData("ping", null)]
    [InlineData("cat ~/.ssh/known_hosts", null)]
    [InlineData("", null)]
    public void Keeps_only_plain_commands_that_dont_run_or_change_things(string text, string? expected) =>
        Assert.Equal(expected, LearnedCommands.Normalize(text));

    [Fact]
    public void A_taught_command_reads_with_its_arguments()
    {
        var learned = LearnedCommands.From(["vault status", "python3", "vault status"]);

        Assert.Equal(["vault status"], learned.Commands);
        Assert.Equal(CommandRisk.ReadOnly, CommandClassifier.Classify("vault status -format=json", learned));
        Assert.Equal(CommandRisk.Changes, CommandClassifier.Classify("vault status", LearnedCommands.Empty));
        Assert.Equal(CommandRisk.Changes, CommandClassifier.Classify("python3 -c 'print(1)'", learned));
    }

    [Theory]
    [InlineData("vault status -o file")]
    [InlineData("vault status > file")]
    [InlineData("vault status; reboot")]
    [InlineData("vault secrets list")]
    public void A_taught_command_still_changes_things_with_more(string command) =>
        Assert.Equal(CommandRisk.Changes, CommandClassifier.Classify(command, LearnedCommands.From(["vault status"])));

    [Fact]
    public void A_taught_command_keeps_its_programs_listed_writing_options() =>
        Assert.Equal(CommandRisk.Changes, CommandClassifier.Classify("git notes copy -f a b", LearnedCommands.From(["git notes copy"])));
}
