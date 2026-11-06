using Datapoint.Terminal.Sessions.Ssh;
using Datapoint.Terminal.Sessions.Ssh.Agent;
using Datapoint.Terminal.Sessions.Ssh.Keys;

namespace Datapoint.Terminal.Sessions.Tests.Ssh;

public sealed class LoginInteropTests(OpenSshServer server) : IClassFixture<OpenSshServer>, IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("dpt-keys-").FullName;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private async Task<string> GenerateKeyAsync(string type, string? passphrase = null)
    {
        var path = Path.Combine(_folder, "id_" + type);
        var (generated, output) = await OpenSshServer.RunToolAsync("ssh-keygen", "-q", "-t", type, "-N", passphrase ?? string.Empty, "-C", "test", "-f", path);
        Assert.SkipUnless(generated, "ssh-keygen isn't available: " + output);
        await server.AuthorizeAsync(await File.ReadAllTextAsync(path + ".pub", Token));
        return path;
    }

    [Theory]
    [InlineData("ed25519")]
    [InlineData("ecdsa")]
    [InlineData("rsa")]
    public async Task Logs_in_with_a_key_file_written_by_ssh_keygen(string type)
    {
        server.SkipIfUnavailable();
        var path = await GenerateKeyAsync(type);
        var key = OpenSshKeyFile.Load(path);

        await using var client = await SshClient.ConnectAsync(server.Options(), Token);
        await client.AuthenticateAsync([key.Signer!], new OpenSshServer.ScriptedPrompts(), Token);

        Assert.Null(key.Problem);
        Assert.Equal("test", key.Comment);
        Assert.True(client.IsAuthenticated);
    }

    [Fact]
    public async Task Reports_a_key_protected_by_a_passphrase()
    {
        var path = await GenerateKeyAsync("ed25519", passphrase: "secret phrase");

        var key = OpenSshKeyFile.Load(path);

        Assert.Null(key.Signer);
        Assert.True(key.IsEncrypted);
        Assert.Contains("ssh-add", key.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Logs_in_with_a_key_held_by_ssh_agent()
    {
        server.SkipIfUnavailable();
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Uses a Unix socket for the agent.");
        var path = await GenerateKeyAsync("ed25519");
        var socket = Path.Combine(_folder, "agent.sock");
        var (started, output) = await OpenSshServer.RunToolAsync("ssh-agent", "-a", socket);
        Assert.SkipUnless(started, "ssh-agent isn't available: " + output);
        var pid = output.Split("SSH_AGENT_PID=")[1].Split(';')[0];
        var previous = Environment.GetEnvironmentVariable("SSH_AUTH_SOCK");
        try
        {
            Environment.SetEnvironmentVariable("SSH_AUTH_SOCK", socket);
            await OpenSshServer.RunToolAsync("ssh-add", path);
            Environment.SetEnvironmentVariable("SSH_AUTH_SOCK", previous);
            var keys = await new SshAgentClient(socket).ListKeysAsync(Token);

            await using var client = await SshClient.ConnectAsync(server.Options(), Token);
            await client.AuthenticateAsync(keys, new OpenSshServer.ScriptedPrompts(), Token);

            Assert.Equal("test", Assert.Single(keys).Comment);
            Assert.True(client.IsAuthenticated);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SSH_AUTH_SOCK", previous);
            await OpenSshServer.RunToolAsync("kill", pid);
        }
    }
}
