namespace Datapoint.Terminal.Agent.Tests;

public sealed class NestedSessionTests
{
    [Theory]
    [InlineData("ssh web1", "ssh session to web1")]
    [InlineData("ssh -p 2222 -i ~/.ssh/id_ed25519 joao@203.0.113.10", "ssh session to joao@203.0.113.10")]
    [InlineData("TERM=xterm ssh -J bastion web1", "ssh session to web1")]
    [InlineData("mosh web1", "mosh session to web1")]
    [InlineData("docker exec -it web sh", "container shell")]
    [InlineData("kubectl exec -it -n shop api-0 -- bash", "pod shell")]
    [InlineData("sudo -i", "nested shell")]
    [InlineData("sudo ssh web1", "ssh session to web1")]
    [InlineData("su - postgres", "nested shell")]
    [InlineData("bash -l", "nested shell")]
    [InlineData("PS1='$ ' sh", "nested shell")]
    public void Recognizes_a_command_that_opens_a_session(string commandLine, string session) =>
        Assert.Equal(session, NestedSession.Describe(commandLine));

    [Theory]
    [InlineData("ssh web1 uptime")]
    [InlineData("ssh -p 2222 web1 'df -h'")]
    [InlineData("ssh web1 \"sudo systemctl restart nginx\"")]
    [InlineData("docker exec web ls")]
    [InlineData("kubectl get pods")]
    [InlineData("bash deploy.sh")]
    [InlineData("sudo apt-get update")]
    [InlineData("ls -la")]
    [InlineData(null)]
    public void Leaves_ordinary_commands_alone(string? commandLine) => Assert.Null(NestedSession.Describe(commandLine));
}
