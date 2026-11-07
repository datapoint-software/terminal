namespace Datapoint.Terminal.Sessions.Ssh.Channels;

// A channel as a byte stream, e.g. a direct-tcpip channel carrying another SSH connection through a jump host. It can own
// what the channel runs over, so closing the inner connection also closes the jump host's.
public sealed class SshChannelStream(SshChannel channel, IAsyncDisposable? owned = null) : Stream
{
    private ReadOnlyMemory<byte> _pending;
    private bool _isDisposed;

    public override bool CanRead => true;

    public override bool CanWrite => true;

    public override bool CanSeek => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        while (_pending.IsEmpty)
        {
            var data = await channel.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (data is null)
            {
                return 0;
            }

            // Extended data on a TCP channel isn't part of the stream.
            if (!data.IsError)
            {
                _pending = data.Data;
            }
        }

        var count = Math.Min(buffer.Length, _pending.Length);
        _pending[..count].CopyTo(buffer);
        _pending = _pending[count..];
        return count;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        new(channel.WriteAsync(buffer, cancellationToken));

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        channel.WriteAsync(buffer.AsMemory(offset, count), cancellationToken);

    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException("Read the channel asynchronously.");

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException("Write to the channel asynchronously.");

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        try
        {
            await channel.CloseAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is SshChannelException or SshDisconnectedException or IOException or ObjectDisposedException)
        {
            // Already closed with the connection.
        }

        channel.Dispose();
        if (owned is not null)
        {
            await owned.DisposeAsync().ConfigureAwait(false);
        }

        await base.DisposeAsync().ConfigureAwait(false);
    }

    // Only the channel's resources: closing it and what it owns needs DisposeAsync, which SshClient uses.
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            channel.Dispose();
        }

        base.Dispose(disposing);
    }
}
