using Datapoint.Terminal.Sessions.Ssh.Config;

namespace Datapoint.Terminal.Sessions.Tests.Ssh.Config;

public sealed class SshForwardTests
{
    [Theory]
    [InlineData("8080 localhost:80", null, 8080, "localhost", 80)]
    [InlineData("127.0.0.1:5432 db.internal:5432", "127.0.0.1", 5432, "db.internal", 5432)]
    [InlineData("[::1]:9000 [fd00::5]:9000", "::1", 9000, "fd00::5", 9000)]
    [InlineData("*:8080 web:80", "*", 8080, "web", 80)]
    public void Reads_a_forward(string text, string? bind, int local, string host, int port) =>
        Assert.Equal(new SshForward(bind, local, host, port), SshForward.Parse(text));

    [Theory]
    [InlineData("8080")]
    [InlineData("8080 /var/run/docker.sock")]
    [InlineData("0 localhost:80")]
    [InlineData("8080 localhost:99999")]
    [InlineData("8080 -oProxyCommand=x:80")]
    [InlineData("8080 localhost:80 extra")]
    public void Refuses_what_it_cant_forward(string text) => Assert.Null(SshForward.Parse(text));

    [Theory]
    [InlineData("8080 web:80", false)]
    [InlineData("localhost:8080 web:80", false)]
    [InlineData("*:8080 web:80", true)]
    [InlineData("0.0.0.0:8080 web:80", true)]
    public void Knows_when_a_forward_asks_for_the_network(string text, bool asks) =>
        Assert.Equal(asks, SshForward.Parse(text)!.AsksForNetwork);
}
