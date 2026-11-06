using System.Diagnostics.CodeAnalysis;
using Datapoint.Terminal.Sessions.Ssh.Channels;

namespace Datapoint.Terminal.Sessions.Ssh;

/// <summary>A shell on a remote host: an SSH session channel with a pseudo-terminal.</summary>
public sealed class SshTerminalSession : ITerminalSession
{
    private readonly SshChannel _channel;
    private ReadOnlyMemory<byte> _pending;

    private SshTerminalSession(SshChannel channel)
    {
        _channel = channel;
        Exited = WaitForExitAsync(channel);
    }

    /// <summary>Shell integration over SSH comes with uploading the scripts; until then remote shells have none.</summary>
    public string? ShellIntegrationNonce => null;

    [SuppressMessage("Usage", "VSTHRD003", Justification = "Started in the constructor; completes from the connection's read loop.")]
    public Task<int?> Exited { get; }

    public static async Task<SshTerminalSession> StartAsync(SshClient client, string terminalType, int columns, int rows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        var channel = await client.OpenSessionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await channel.RequestPtyAsync(terminalType, columns, rows, cancellationToken).ConfigureAwait(false);
            await channel.StartShellAsync(cancellationToken).ConfigureAwait(false);
            return new SshTerminalSession(channel);
        }
        catch
        {
            await channel.CloseAsync(CancellationToken.None).ConfigureAwait(false);
            channel.Dispose();
            throw;
        }
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
