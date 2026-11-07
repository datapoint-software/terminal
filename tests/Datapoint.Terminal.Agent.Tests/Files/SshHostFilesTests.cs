using System.Text;
using Datapoint.Terminal.Agent.Files;
using Datapoint.Terminal.Sessions.Tests.Ssh;

namespace Datapoint.Terminal.Agent.Tests.Files;

public sealed class SshHostFilesTests(OpenSshServer server) : IClassFixture<OpenSshServer>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Reads_stages_and_reports_what_needs_sudo_over_a_real_connection()
    {
        server.SkipIfUnavailable();
        await using var client = await OpenSshServer.ConnectWithPasswordAsync(server.Options());
        var files = new SshHostFiles(client);

        var staged = await files.StageAsync("it's\tnew\n"u8.ToArray(), Token);
        var back = await files.ReadAsync(staged, 100, Token);
        var system = await files.ReadAsync("/etc/passwd", 10, Token);
        var missing = await files.ReadAsync("~/new.conf", 10, Token);
        var root = await files.ReadAsync("/etc/new.conf", 10, Token);

        Assert.StartsWith("~/.datapoint-terminal/staging/", staged, StringComparison.Ordinal);
        Assert.Equal((true, "it's\tnew\n", true, false), (back.Exists, Encoding.UTF8.GetString(back.Content), back.IsWritable, back.IsTruncated));
        Assert.Equal((true, 10, false, true), (system.Exists, system.Content.Length, system.IsWritable, system.IsTruncated));
        Assert.Equal((false, true), (missing.Exists, missing.IsWritable));
        Assert.Equal((false, false), (root.Exists, root.IsWritable));
        await Assert.ThrowsAsync<IOException>(() => files.ReadAsync("/etc", 10, Token));
        await Assert.ThrowsAsync<IOException>(() => files.ReadAsync("/nowhere/x", 10, Token));
    }
}
