using System.Text;
using Datapoint.Terminal.Sessions.Ssh;

namespace Datapoint.Terminal.Sessions.Tests.Ssh;

// Its own server, since each test changes the tester's login shell.
public sealed class RemoteShellIntegrationTests(OpenSshServer server) : IClassFixture<OpenSshServer>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("/bin/bash")]
    [InlineData("/bin/zsh")]
    [InlineData("/usr/bin/fish")]
    public async Task Integrates_the_hosts_shell_and_leaves_nothing_behind(string shell)
    {
        server.SkipIfUnavailable();
        await server.SetShellAsync(shell);
        await using var client = await OpenSshServer.ConnectWithPasswordAsync(server.Options());
        await using var session = await SshTerminalSession.StartAsync(client, "xterm-256color", 80, 24, Token, shellIntegration: true);

        Assert.NotNull(session.ShellIntegrationNonce);
        var output = await RunAsync(session, "echo hi there", until: "\u001b]133;D;0");

        Assert.Contains($"\u001b]633;E;echo hi there;{session.ShellIntegrationNonce}\u0007", output, StringComparison.Ordinal);
        Assert.Contains("hi there", output, StringComparison.Ordinal);
        Assert.DoesNotContain("__dpt", output, StringComparison.Ordinal);
        Assert.DoesNotContain(session.ShellIntegrationNonce!, output.Replace($";{session.ShellIntegrationNonce}\u0007", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Equal(string.Empty, (await server.RunAsync("find /tmp -mindepth 1 -maxdepth 1 -user tester")).Trim());
        Assert.DoesNotContain(session.ShellIntegrationNonce!, await server.RunAsync("ps -o args; cat /proc/[0-9]*/cmdline 2>/dev/null | tr '\\0' ' '"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gives_other_shells_their_usual_login()
    {
        server.SkipIfUnavailable();
        await server.SetShellAsync("/bin/sh");
        await using var client = await OpenSshServer.ConnectWithPasswordAsync(server.Options());
        await using var session = await SshTerminalSession.StartAsync(client, "xterm-256color", 80, 24, Token, shellIntegration: true);

        Assert.Null(session.ShellIntegrationNonce);
        var output = await RunAsync(session, "echo plain-$((40+2))", until: "plain-42");
        Assert.DoesNotContain("633;P", output, StringComparison.Ordinal);
    }

    private static async Task<string> RunAsync(SshTerminalSession session, string command, string until)
    {
        await session.WriteAsync(Encoding.UTF8.GetBytes(command + "\n"), Token);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        var output = new StringBuilder();
        var buffer = new byte[4096];
        while (!output.ToString().Contains(until, StringComparison.Ordinal))
        {
            var count = await session.ReadAsync(buffer, timeout.Token);
            if (count == 0)
            {
                break;
            }

            output.Append(Encoding.UTF8.GetString(buffer, 0, count));
        }

        return output.ToString();
    }
}
