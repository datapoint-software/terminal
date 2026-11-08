using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using Datapoint.Terminal.Security.Cryptography;
using Datapoint.Terminal.Sessions.Ssh;
using Datapoint.Terminal.Sessions.Ssh.Authentication;

namespace Datapoint.Terminal.Sessions.Tests.Ssh;

// Tests that need it skip when Docker or the image is missing. Build the image with:
// docker build -t datapoint-terminal-sshd:test tests/ssh-server
public sealed class OpenSshServer : IAsyncLifetime
{
    public const string Image = "datapoint-terminal-sshd:test";
    public const string UserName = "tester";
    public const string Password = "correct horse battery staple";

    private static readonly TimeSpan ToolTimeLimit = TimeSpan.FromMinutes(2);

    private string? _container;

    public int Port { get; private set; }

    public string? Unavailable { get; private set; }

    public Ed25519PrivateKey UserKey { get; } = Ed25519PrivateKey.FromSeed(RandomBytes.Create(Ed25519PrivateKey.SeedSize));

    public async ValueTask InitializeAsync()
    {
        var (started, output) = await RunAsync("docker", "run", "-d", "--rm", "-p", "127.0.0.1::22", Image);
        if (!started)
        {
            Unavailable = $"Docker or the {Image} image isn't available: {output}";
            return;
        }

        _container = output.Trim();
        var (_, port) = await RunAsync("docker", "port", _container, "22/tcp");
        Port = int.Parse(port.Trim().Split(':')[^1], CultureInfo.InvariantCulture);
        var publicKey = "ssh-ed25519 " + Convert.ToBase64String(InMemorySigner.ForEd25519(UserKey).PublicKeyBlob.Span) + " test";
        await RunAsync("docker", "exec", _container, "sh", "-c", $"echo '{publicKey}' > /home/tester/.ssh/authorized_keys && chown tester:tester /home/tester/.ssh/authorized_keys && chmod 600 /home/tester/.ssh/authorized_keys");
        await WaitUntilListeningAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is null)
        {
            return;
        }

        await RunAsync("docker", "rm", "-f", _container);
    }

    public void SkipIfUnavailable()
    {
        if (Unavailable is null)
        {
            return;
        }

        Assert.Skip(Unavailable);
    }

    public async Task AuthorizeAsync(string publicKeyLine)
    {
        await RunAsync("docker", "exec", _container!, "sh", "-c", $"echo '{publicKeyLine.Trim()}' >> /home/tester/.ssh/authorized_keys");
    }

    // The tester's login shell, e.g. "/bin/zsh".
    public async Task SetShellAsync(string shell) =>
        await RunAsync("docker", "exec", _container!, "sed", "-i", $"s#^tester:\\(.*\\):[^:]*$#tester:\\1:{shell}#", "/etc/passwd");

    public async Task<string> RunAsync(string command) => (await RunAsync("docker", "exec", _container!, "sh", "-c", command)).Output;

    public static Task<(bool Succeeded, string Output)> RunToolAsync(string fileName, params string[] arguments) => RunAsync(fileName, arguments);

    public SshConnectionOptions Options(IHostKeyVerifier? verifier = null) => new()
    {
        Host = "127.0.0.1",
        Port = Port,
        UserName = UserName,
        HostKeyVerifier = verifier ?? new AcceptingVerifier(),
    };

    public static async Task<SshClient> ConnectWithPasswordAsync(SshConnectionOptions options)
    {
        var client = await SshClient.ConnectAsync(options, TestContext.Current.CancellationToken);
        await client.AuthenticateAsync([], new ScriptedPrompts(Password), TestContext.Current.CancellationToken);
        return client;
    }

    private async Task WaitUntilListeningAsync()
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            try
            {
                // Each attempt has a time limit, so a connection that's accepted but never answered can't hang the run.
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var socket = new TcpClient();
                await socket.ConnectAsync("127.0.0.1", Port, timeout.Token);
                var buffer = new byte[4];
                if (await socket.GetStream().ReadAsync(buffer, timeout.Token) == 4)
                {
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                // Accepted but sshd didn't answer in time.
            }
            catch (SocketException)
            {
                // Not listening yet.
            }
            catch (IOException)
            {
                // Docker accepted the connection before sshd was ready.
            }

            await Task.Delay(100);
        }

        Unavailable = "The SSH server didn't start.";
    }

    private static async Task<(bool Succeeded, string Output)> RunAsync(string fileName, params string[] arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(fileName, arguments) { RedirectStandardOutput = true, RedirectStandardInput = true, RedirectStandardError = true, UseShellExecute = false })!;
            process.StandardInput.Close();

            // Both pipes are read at once, so a tool that fills one while the other is being read can't block, and a
            // tool that never finishes fails its test after the time limit instead of hanging the whole run.
            using var timeout = new CancellationTokenSource(ToolTimeLimit);
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var error = process.StandardError.ReadToEndAsync(timeout.Token);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
                await Task.WhenAll(output, error);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                return (false, $"{fileName} {string.Join(' ', arguments)} didn't finish within {ToolTimeLimit.TotalSeconds:0} seconds.");
            }

            return (process.ExitCode == 0, process.ExitCode == 0 ? await output : await error);
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            return (false, exception.Message);
        }
    }

    public sealed class AcceptingVerifier : IHostKeyVerifier
    {
        public string? SeenFingerprint { get; private set; }

        public Task<HostKeyDecision> VerifyAsync(string host, int port, Sessions.Ssh.Keys.SshPublicKey key, CancellationToken cancellationToken)
        {
            SeenFingerprint = key.Fingerprint;
            return Task.FromResult(HostKeyDecision.Accepted);
        }
    }

    public sealed class RejectingVerifier : IHostKeyVerifier
    {
        public Task<HostKeyDecision> VerifyAsync(string host, int port, Sessions.Ssh.Keys.SshPublicKey key, CancellationToken cancellationToken) =>
            Task.FromResult(HostKeyDecision.Rejected);
    }

    public sealed class ScriptedPrompts(params string?[] passwords) : IAuthenticationPrompts
    {
        private int _next;

        public int PasswordRequests => _next;

        public void ShowBanner(string text)
        {
        }

        public Task<string?> ReadPasswordAsync(string userName, string host, CancellationToken cancellationToken) =>
            Task.FromResult(_next < passwords.Length ? passwords[_next++] : null);

        public Task<IReadOnlyList<string>?> AnswerAsync(KeyboardInteractiveRequest request, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>?>(null);
    }

}
