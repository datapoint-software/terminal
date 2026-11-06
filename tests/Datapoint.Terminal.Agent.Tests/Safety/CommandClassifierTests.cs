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
    public void Treats_anything_else_as_a_change(string command) => Assert.Equal(CommandRisk.Changes, CommandClassifier.Classify(command));

    [Theory]
    [InlineData("cat ~/.ssh/known_hosts")]
    [InlineData("echo key >> ~/.ssh/authorized_keys")]
    [InlineData("ssh-keygen -R example.com")]
    [InlineData("vi ~/.ssh/config")]
    public void Always_asks_for_what_decides_trust(string command) => Assert.Equal(CommandRisk.AlwaysAsk, CommandClassifier.Classify(command));
}
