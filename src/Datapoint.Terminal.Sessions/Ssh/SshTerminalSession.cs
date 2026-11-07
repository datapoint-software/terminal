using System.Diagnostics.CodeAnalysis;
using Datapoint.Terminal.Sessions.ShellIntegration;
using Datapoint.Terminal.Sessions.Ssh.Channels;

namespace Datapoint.Terminal.Sessions.Ssh;

public sealed class SshTerminalSession : ITerminalSession
{
    private static readonly TimeSpan s_bootstrapTimeout = TimeSpan.FromSeconds(10);

    private readonly SshChannel _channel;
    private readonly SshClient? _ownedClient;
    private ReadOnlyMemory<byte> _pending;

    private SshTerminalSession(SshChannel channel, SshClient? ownedClient, string? nonce = null, ReadOnlyMemory<byte> pending = default)
    {
        _channel = channel;
        _ownedClient = ownedClient;
        _pending = pending;
        ShellIntegrationNonce = nonce;
        Exited = WaitForExitAsync(channel);
    }

    // The connection, when the session owns it, for commands and file access beside the terminal.
    public SshClient? Client => _ownedClient;

    public string? ShellIntegrationNonce { get; }

    [SuppressMessage("Usage", "VSTHRD003", Justification = "Started in the constructor; completes from the connection's read loop.")]
    public Task<int?> Exited { get; }

    // With a command, e.g. to open a shell in a container on the host, the session runs it in place of the login shell.
    // With shell integration, the login shell is started with the scripts if it's one they support (see RemoteShellIntegration);
    // if anything goes wrong on the way, the session is an ordinary login shell.
    public static async Task<SshTerminalSession> StartAsync(SshClient client, string terminalType, int columns, int rows, CancellationToken cancellationToken, bool ownsClient = false, string? command = null, bool shellIntegration = false)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (command is null && shellIntegration && await TryStartIntegratedAsync(client, terminalType, columns, rows, ownsClient, cancellationToken).ConfigureAwait(false) is { } integrated)
        {
            return integrated;
        }

        var channel = await client.OpenSessionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await channel.RequestPtyAsync(terminalType, columns, rows, cancellationToken).ConfigureAwait(false);
            await (command is null ? channel.StartShellAsync(cancellationToken) : channel.ExecuteAsync(command, cancellationToken)).ConfigureAwait(false);
            return new SshTerminalSession(channel, ownsClient ? client : null);
        }
        catch
        {
            await channel.CloseAsync(CancellationToken.None).ConfigureAwait(false);
            channel.Dispose();
            throw;
        }
    }

    private static async Task<SshTerminalSession?> TryStartIntegratedAsync(SshClient client, string terminalType, int columns, int rows, bool ownsClient, CancellationToken cancellationToken)
    {
        var channel = await client.OpenSessionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await channel.RequestPtyAsync(terminalType, columns, rows, cancellationToken).ConfigureAwait(false);
            await channel.ExecuteAsync(RemoteShellIntegration.Command, cancellationToken).ConfigureAwait(false);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(s_bootstrapTimeout);
            var seen = new List<byte>();
            while (seen.Count < 64 * 1024 && await channel.ReadAsync(timeout.Token).ConfigureAwait(false) is { } data)
            {
                seen.AddRange(data.Data.Span);
                var output = seen.ToArray();
                var (isReady, index, length) = RemoteShellIntegration.FindMarker(output);
                if (isReady is not { } ready)
                {
                    continue;
                }

                byte[] shown = [.. output.AsSpan(0, index), .. output.AsSpan(index + length)];
                var nonce = ready ? Convert.ToHexStringLower(Security.Cryptography.RandomBytes.Create(16)) : null;
                if (nonce is not null)
                {
                    await channel.WriteAsync(RemoteShellIntegration.Payload(nonce), cancellationToken).ConfigureAwait(false);
                }

                return new SshTerminalSession(channel, ownsClient ? client : null, nonce, shown);
            }
        }
        catch (SshChannelException)
        {
            // The host refused the command, e.g. a forced command or a device without a POSIX shell.
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The bootstrap never answered.
        }

        try
        {
            await channel.CloseAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is SshChannelException or SshDisconnectedException)
        {
            // Already closed by the host.
        }

        channel.Dispose();
        return null;
    }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (_pending.IsEmpty)
        {
            var data = await _channel.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (data is null)
            {
                return 0;
            }

            _pending = data.Data;
        }

        var count = Math.Min(buffer.Length, _pending.Length);
        _pending[..count].CopyTo(buffer);
        _pending = _pending[count..];
        return count;
    }

    public Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken) => _channel.WriteAsync(data, cancellationToken);

    public Task ResizeAsync(int columns, int rows, CancellationToken cancellationToken) => _channel.ResizeAsync(columns, rows, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _channel.CloseAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is SshChannelException or SshDisconnectedException)
        {
            // Already closed.
        }

        _channel.Dispose();
        if (_ownedClient is not null)
        {
            await _ownedClient.DisposeAsync().ConfigureAwait(false);
        }
    }

    [SuppressMessage("Usage", "VSTHRD003", Justification = "The channel completes from the connection's read loop, never on the caller's context.")]
    private static async Task<int?> WaitForExitAsync(SshChannel channel)
    {
        try
        {
            await channel.Completion.ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is SshChannelException or SshDisconnectedException or Protocol.SshProtocolException)
        {
            return null;
        }

        return channel.ExitCode;
    }
}
