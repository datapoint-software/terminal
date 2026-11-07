using System.Text;
using Datapoint.Terminal.Sessions.Ssh;
using Datapoint.Terminal.Sessions.Ssh.Authentication;
using Datapoint.Terminal.Sessions.Ssh.Channels;
using Datapoint.Terminal.Sessions.Ssh.Protocol;
using Datapoint.Terminal.Sessions.Ssh.Transport;

namespace Datapoint.Terminal.Sessions.Tests.Ssh;

public sealed class OpenSshInteropTests(OpenSshServer server) : IClassFixture<OpenSshServer>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static TheoryData<string> KeyExchanges =>
    [
        AlgorithmNames.MlKem768X25519,
        AlgorithmNames.Curve25519,
        AlgorithmNames.EcdhP256,
        AlgorithmNames.EcdhP384,
        AlgorithmNames.EcdhP521,
    ];

    public static TheoryData<string, string> Ciphers => new()
    {
        { AlgorithmNames.ChaCha20Poly1305, AlgorithmNames.HmacSha256Etm },
        { AlgorithmNames.Aes256Gcm, AlgorithmNames.HmacSha256Etm },
        { AlgorithmNames.Aes128Gcm, AlgorithmNames.HmacSha256Etm },
        { AlgorithmNames.Aes256Ctr, AlgorithmNames.HmacSha256Etm },
        { AlgorithmNames.Aes128Ctr, AlgorithmNames.HmacSha512Etm },
    };

    public static TheoryData<string> HostKeys =>
    [
        AlgorithmNames.Ed25519,
        AlgorithmNames.EcdsaP256,
        AlgorithmNames.RsaSha512,
        AlgorithmNames.RsaSha256,
    ];

    private static async Task<(string Output, int? ExitCode)> RunAsync(SshClient client, string command)
    {
        var channel = await client.OpenSessionAsync(Token);
        await channel.ExecuteAsync(command, Token);
        var output = new StringBuilder();
        while (await channel.ReadAsync(Token) is { } data)
        {
            output.Append(Encoding.UTF8.GetString(data.Data.Span));
        }

        await channel.Completion.WaitAsync(Token);
        channel.Dispose();
        return (output.ToString(), channel.ExitCode);
    }

    [Theory]
    [MemberData(nameof(KeyExchanges))]
    public async Task Connects_with_each_key_exchange(string keyExchange)
    {
        server.SkipIfUnavailable();
        var options = server.Options() with { Algorithms = AlgorithmPreferences.Standard with { KeyExchange = [keyExchange] } };

        await using var client = await OpenSshServer.ConnectWithPasswordAsync(options);

        Assert.Equal(keyExchange, client.Algorithms.KeyExchange);
        Assert.Equal(("hello\n", (int?)0), await RunAsync(client, "echo hello"));
    }

    [Theory]
    [MemberData(nameof(Ciphers))]
    public async Task Connects_with_each_cipher(string cipher, string mac)
    {
        server.SkipIfUnavailable();
        var options = server.Options() with { Algorithms = AlgorithmPreferences.Standard with { Cipher = [cipher], Mac = [mac] } };

        await using var client = await OpenSshServer.ConnectWithPasswordAsync(options);

        Assert.Equal(cipher, client.Algorithms.CipherClientToServer);
        Assert.Equal(("hello\n", (int?)0), await RunAsync(client, "echo hello"));
    }

    [Theory]
    [MemberData(nameof(HostKeys))]
    public async Task Verifies_each_host_key_type(string hostKey)
    {
        server.SkipIfUnavailable();
        var options = server.Options() with { Algorithms = AlgorithmPreferences.Standard with { HostKey = [hostKey] } };

        await using var client = await SshClient.ConnectAsync(options, Token);

        Assert.Equal(hostKey, client.Algorithms.HostKey);
    }

    [Fact]
    public async Task Uses_strict_key_exchange_with_openssh()
    {
        server.SkipIfUnavailable();

        await using var client = await SshClient.ConnectAsync(server.Options(), Token);

        Assert.True(client.IsStrictKeyExchange);
        Assert.StartsWith("SSH-2.0-OpenSSH_", client.ServerVersion, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fails_when_the_host_key_is_rejected()
    {
        server.SkipIfUnavailable();

        await Assert.ThrowsAsync<SshProtocolException>(() => SshClient.ConnectAsync(server.Options(new OpenSshServer.RejectingVerifier()), Token));
    }

    [Fact]
    public async Task Logs_in_with_a_key()
    {
        server.SkipIfUnavailable();
        await using var client = await SshClient.ConnectAsync(server.Options(), Token);
        var prompts = new OpenSshServer.ScriptedPrompts();

        await client.AuthenticateAsync([InMemorySigner.ForEd25519(server.UserKey)], prompts, Token);

        Assert.True(client.IsAuthenticated);
        Assert.Equal(0, prompts.PasswordRequests);
        Assert.Equal(("tester\n", (int?)0), await RunAsync(client, "whoami"));
    }

    [Fact]
    public async Task Asks_again_after_a_wrong_password()
    {
        server.SkipIfUnavailable();
        await using var client = await SshClient.ConnectAsync(server.Options(), Token);
        var prompts = new OpenSshServer.ScriptedPrompts("wrong", OpenSshServer.Password);

        await client.AuthenticateAsync([], prompts, Token);

        Assert.Equal(2, prompts.PasswordRequests);
    }

    [Fact]
    public async Task Fails_when_the_user_gives_up()
    {
        server.SkipIfUnavailable();
        await using var client = await SshClient.ConnectAsync(server.Options(), Token);

        await Assert.ThrowsAsync<SshAuthenticationException>(() => client.AuthenticateAsync([], new OpenSshServer.ScriptedPrompts("wrong"), Token));
    }

    [Fact]
    public async Task Reports_exit_codes_and_standard_error()
    {
        server.SkipIfUnavailable();
        await using var client = await OpenSshServer.ConnectWithPasswordAsync(server.Options());
        var channel = await client.OpenSessionAsync(Token);
        await channel.ExecuteAsync("echo oops >&2; exit 3", Token);

        var data = await channel.ReadAsync(Token);
        await channel.Completion.WaitAsync(Token);

        Assert.True(data!.IsError);
        Assert.Equal("oops\n", Encoding.UTF8.GetString(data.Data.Span));
        Assert.Equal(3, channel.ExitCode);
    }

    [Fact]
    public async Task Runs_a_command_beside_the_terminal_with_input_and_a_limit()
    {
        server.SkipIfUnavailable();
        await using var client = await OpenSshServer.ConnectWithPasswordAsync(server.Options());

        var copied = await client.RunAsync("tr a-z A-Z; echo done >&2; exit 4", "hello\tworld\n"u8.ToArray(), 100, Token);
        var limited = await client.RunAsync("head -c 5000 /dev/zero", ReadOnlyMemory<byte>.Empty, 1000, Token);

        Assert.Equal(("HELLO\tWORLD\n", "done\n", 4, false), (Encoding.UTF8.GetString(copied.Output), copied.Error, copied.ExitCode, copied.IsTruncated));
        Assert.Equal((1000, 0, true), (limited.Output.Length, limited.ExitCode ?? -1, limited.IsTruncated));
    }

    [Fact]
    public async Task Runs_an_interactive_shell_in_a_pty()
    {
        server.SkipIfUnavailable();
        await using var client = await OpenSshServer.ConnectWithPasswordAsync(server.Options());
        var channel = await client.OpenSessionAsync(Token);
        await channel.RequestPtyAsync("xterm-256color", 80, 24, Token);
        await channel.StartShellAsync(Token);

        await channel.WriteAsync("echo $((6*7)) $TERM; exit\n"u8.ToArray(), Token);
        var output = new StringBuilder();
        while (await channel.ReadAsync(Token) is { } data)
        {
            output.Append(Encoding.UTF8.GetString(data.Data.Span));
        }

        Assert.Contains("42 xterm-256color", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rekeys_during_a_large_transfer()
    {
        server.SkipIfUnavailable();
        var options = server.Options() with { RekeyBytes = 256 * 1024 };
        await using var client = await OpenSshServer.ConnectWithPasswordAsync(options);
        var channel = await client.OpenSessionAsync(Token);
        await channel.ExecuteAsync("head -c 3000000 /dev/zero", Token);

        long total = 0;
        while (await channel.ReadAsync(Token) is { } data)
        {
            total += data.Data.Length;
        }

        Assert.Equal(3_000_000, total);
        Assert.True(client.CompletedKeyExchanges > 1, $"Expected a rekey, saw {client.CompletedKeyExchanges} key exchanges.");
    }

    [Fact]
    public async Task Sends_input_larger_than_the_servers_window()
    {
        server.SkipIfUnavailable();
        await using var client = await OpenSshServer.ConnectWithPasswordAsync(server.Options());
        var channel = await client.OpenSessionAsync(Token);
        await channel.ExecuteAsync("wc -c", Token);

        await channel.WriteAsync(new byte[5_000_000], Token);
        await channel.SendEofAsync(Token);
        var output = new StringBuilder();
        while (await channel.ReadAsync(Token) is { } data)
        {
            output.Append(Encoding.UTF8.GetString(data.Data.Span));
        }

        Assert.Equal("5000000", output.ToString().Trim());
    }

    [Fact]
    public async Task Runs_a_terminal_session_until_the_shell_exits()
    {
        server.SkipIfUnavailable();
        await using var client = await OpenSshServer.ConnectWithPasswordAsync(server.Options());
        await using var session = await SshTerminalSession.StartAsync(client, "xterm-256color", 80, 24, Token);

        await session.WriteAsync("exit 7\n"u8.ToArray(), Token);
        var buffer = new byte[16];
        while (await session.ReadAsync(buffer, Token) > 0)
        {
        }

        Assert.Equal(7, await session.Exited.WaitAsync(Token));
    }

    [Fact]
    public async Task Runs_a_command_in_place_of_the_login_shell()
    {
        server.SkipIfUnavailable();
        await using var client = await OpenSshServer.ConnectWithPasswordAsync(server.Options());
        await using var session = await SshTerminalSession.StartAsync(client, "xterm-256color", 80, 24, Token, command: "printf 'in %s' container; exit 3");

        var output = new StringBuilder();
        var buffer = new byte[64];
        int count;
        while ((count = await session.ReadAsync(buffer, Token)) > 0)
        {
            output.Append(Encoding.UTF8.GetString(buffer, 0, count));
        }

        Assert.Contains("in container", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(3, await session.Exited.WaitAsync(Token));
    }

    // The server is its own jump host: the second connection runs inside a direct-tcpip channel of the first.
    [Fact]
    public async Task Connects_through_a_jump_host_and_closes_it_with_the_target()
    {
        server.SkipIfUnavailable();
        var jump = await OpenSshServer.ConnectWithPasswordAsync(server.Options());
        var channel = await jump.OpenDirectTcpIpAsync("127.0.0.1", 22, Token);
        var stream = new SshChannelStream(channel, jump);
        var target = await SshClient.ConnectAsync(server.Options() with { Port = 22 }, stream, Token);
        await target.AuthenticateAsync([], new OpenSshServer.ScriptedPrompts(OpenSshServer.Password), Token);

        var result = await target.RunAsync("echo inside", ReadOnlyMemory<byte>.Empty, 1024, Token);
        await target.DisposeAsync();

        Assert.Equal("inside", Encoding.UTF8.GetString(result.Output).Trim());
        await Assert.ThrowsAnyAsync<Exception>(() => jump.OpenSessionAsync(Token));
    }

    [Fact]
    public async Task Reports_a_target_the_jump_host_cant_reach()
    {
        server.SkipIfUnavailable();
        await using var jump = await OpenSshServer.ConnectWithPasswordAsync(server.Options());

        await Assert.ThrowsAsync<SshChannelException>(() => jump.OpenDirectTcpIpAsync("127.0.0.1", 1, Token));
    }

    // The forward leads back to the server's own sshd, whose greeting proves the bytes went through the tunnel.
    [Fact]
    public async Task Forwards_a_local_port_to_the_server_side()
    {
        server.SkipIfUnavailable();
        await using var client = await OpenSshServer.ConnectWithPasswordAsync(server.Options());
        await using var forward = SshLocalForward.Start(client, 0, "127.0.0.1", 22);

        using var socket = new System.Net.Sockets.TcpClient();
        await socket.ConnectAsync(System.Net.IPAddress.Loopback, forward.LocalPort, Token);
        var buffer = new byte[64];
        var count = await socket.GetStream().ReadAsync(buffer, Token);

        Assert.StartsWith("SSH-2.0-OpenSSH", Encoding.ASCII.GetString(buffer, 0, count), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Uploads_and_downloads_a_file_exactly()
    {
        server.SkipIfUnavailable();
        await using var client = await OpenSshServer.ConnectWithPasswordAsync(server.Options());
        var content = new byte[300_000];
        System.Security.Cryptography.RandomNumberGenerator.Fill(content);
        using var source = new MemoryStream(content);

        var sent = await client.UploadAsync(source, "~/it's here.bin", overwrite: false, progress: null, Token);
        using var copy = new MemoryStream();
        var received = await client.DownloadAsync("~/it's here.bin", copy, progress: null, Token);

        Assert.Equal(content.Length, sent);
        Assert.Equal(content.Length, received);
        Assert.Equal(content, copy.ToArray());
        var leftovers = await client.RunAsync("ls -a ~ | grep -c datapoint-partial || true", ReadOnlyMemory<byte>.Empty, 64, Token);
        Assert.Equal("0", Encoding.UTF8.GetString(leftovers.Output).Trim());
    }

    [Fact]
    public async Task Asks_before_replacing_a_file()
    {
        server.SkipIfUnavailable();
        await using var client = await OpenSshServer.ConnectWithPasswordAsync(server.Options());
        await client.UploadAsync(new MemoryStream("first"u8.ToArray()), "~/replace.txt", overwrite: true, progress: null, Token);

        await Assert.ThrowsAsync<RemoteFileExistsException>(() => client.UploadAsync(new MemoryStream("second"u8.ToArray()), "~/replace.txt", overwrite: false, progress: null, Token));
        await client.UploadAsync(new MemoryStream("third"u8.ToArray()), "~/replace.txt", overwrite: true, progress: null, Token);

        using var copy = new MemoryStream();
        await client.DownloadAsync("~/replace.txt", copy, progress: null, Token);
        Assert.Equal("third", Encoding.UTF8.GetString(copy.ToArray()));
    }

    [Fact]
    public async Task Reports_a_file_that_isnt_there()
    {
        server.SkipIfUnavailable();
        await using var client = await OpenSshServer.ConnectWithPasswordAsync(server.Options());

        var exception = await Assert.ThrowsAsync<IOException>(() => client.DownloadAsync("/nowhere/at-all", Stream.Null, progress: null, Token));

        Assert.Contains("No such file", exception.Message, StringComparison.Ordinal);
    }
}
