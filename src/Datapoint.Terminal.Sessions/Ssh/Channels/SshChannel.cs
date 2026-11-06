using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using Datapoint.Terminal.Sessions.Ssh.Protocol;

namespace Datapoint.Terminal.Sessions.Ssh.Channels;

// Output is buffered only up to the window we grant, so a slow reader slows the server down instead of filling memory.
public sealed class SshChannel : IDisposable
{
    public const uint InitialWindow = 2 * 1024 * 1024;
    public const uint MaxPacket = 32 * 1024;

    private readonly Func<byte[], CancellationToken, Task> _send;
    private readonly Lock _lock = new();
    private readonly Channel<SshChannelData> _output = Channel.CreateUnbounded<SshChannelData>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    private readonly Queue<TaskCompletionSource<bool>> _pendingReplies = new();
    private readonly TaskCompletionSource _opened = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private TaskCompletionSource _windowGrown = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private long _remoteWindow;
    private uint _remoteMaxPacket;
    private long _localWindow = InitialWindow;
    private long _consumed;
    private bool _closeSent;
    private bool _eofSent;

    internal SshChannel(uint localId, Func<byte[], CancellationToken, Task> send)
    {
        LocalId = localId;
        _send = send;
    }

    public uint LocalId { get; }

    public uint RemoteId { get; private set; }

    public int? ExitCode { get; private set; }

    public string? ExitSignal { get; private set; }

    [SuppressMessage("Usage", "VSTHRD003", Justification = "Completes from the read loop, which never waits on the caller's context.")]
    public Task Completion => _closed.Task;

    public Task RequestPtyAsync(string terminalType, int columns, int rows, CancellationToken cancellationToken)
    {
        // Terminal modes: IUTF8 on, then TTY_OP_END.
        var modes = new SshWriter().WriteByte(42).WriteUInt32(1).WriteByte(0).ToArray();
        var request = BeginRequest("pty-req", wantReply: true)
            .WriteString(terminalType)
            .WriteUInt32((uint)columns)
            .WriteUInt32((uint)rows)
            .WriteUInt32(0)
            .WriteUInt32(0)
            .WriteString(modes);
        return SendRequestAsync(request.ToArray(), "a terminal", cancellationToken);
    }

    public Task StartShellAsync(CancellationToken cancellationToken) =>
        SendRequestAsync(BeginRequest("shell", wantReply: true).ToArray(), "a shell", cancellationToken);

    public Task ExecuteAsync(string command, CancellationToken cancellationToken) =>
        SendRequestAsync(BeginRequest("exec", wantReply: true).WriteString(command).ToArray(), "the command", cancellationToken);

    public Task StartSubsystemAsync(string name, CancellationToken cancellationToken) =>
        SendRequestAsync(BeginRequest("subsystem", wantReply: true).WriteString(name).ToArray(), $"the {name} subsystem", cancellationToken);

    public Task ResizeAsync(int columns, int rows, CancellationToken cancellationToken)
    {
        var request = BeginRequest("window-change", wantReply: false)
            .WriteUInt32((uint)columns)
            .WriteUInt32((uint)rows)
            .WriteUInt32(0)
            .WriteUInt32(0);
        return _send(request.ToArray(), cancellationToken);
    }

    public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var remaining = data;
            while (!remaining.IsEmpty)
            {
                var count = await ReserveWindowAsync(remaining.Length, cancellationToken).ConfigureAwait(false);
                var message = new SshWriter().WriteByte(MessageNumber.ChannelData).WriteUInt32(RemoteId).WriteString(remaining.Span[..count]).ToArray();
                await _send(message, cancellationToken).ConfigureAwait(false);
                remaining = remaining[count..];
            }
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async ValueTask<SshChannelData?> ReadAsync(CancellationToken cancellationToken)
    {
        while (await _output.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (_output.Reader.TryRead(out var item))
            {
                await ConsumeAsync(item.Data.Length, cancellationToken).ConfigureAwait(false);
                return item;
            }
        }

        return null;
    }

    public async Task SendEofAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_eofSent || _closeSent)
            {
                return;
            }

            _eofSent = true;
        }

        await _send(Message(MessageNumber.ChannelEof), cancellationToken).ConfigureAwait(false);
    }

    public async Task CloseAsync(CancellationToken cancellationToken)
    {
        if (!TryMarkCloseSent())
        {
            return;
        }

        await _send(Message(MessageNumber.ChannelClose), cancellationToken).ConfigureAwait(false);
    }

    internal Task WaitUntilOpenAsync(CancellationToken cancellationToken) => _opened.Task.WaitAsync(cancellationToken);

    internal void OnOpenConfirmation(ref SshReader reader)
    {
        RemoteId = reader.ReadUInt32();
        var window = reader.ReadUInt32();
        _remoteMaxPacket = Math.Max(1, reader.ReadUInt32());
        lock (_lock)
        {
            _remoteWindow = window;
        }

        _opened.TrySetResult();
    }

    internal void OnOpenFailure(ref SshReader reader)
    {
        var code = reader.ReadUInt32();
        var description = ServerText.Sanitize(reader.ReadUtf8String(), 512);
        var failure = new SshChannelException($"The server refused the channel ({code}): {description}");
        _opened.TrySetException(failure);
        Fail(failure);
    }

    internal void OnWindowAdjust(ref SshReader reader)
    {
        var added = reader.ReadUInt32();
        lock (_lock)
        {
            _remoteWindow += added;
            if (_remoteWindow > uint.MaxValue)
            {
                throw new SshProtocolException("The server grew a channel window past its limit.", DisconnectReason.ProtocolError);
            }

            _windowGrown.TrySetResult();
            _windowGrown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    internal void OnData(ReadOnlySpan<byte> data, bool isError)
    {
        lock (_lock)
        {
            if (data.Length > MaxPacket || data.Length > _localWindow)
            {
                throw new SshProtocolException("The server sent more channel data than the window allows.", DisconnectReason.ProtocolError);
            }

            _localWindow -= data.Length;
        }

        _output.Writer.TryWrite(new SshChannelData(data.ToArray(), isError));
    }

    internal void OnEof() => _output.Writer.TryComplete();

    internal void OnReply(bool success)
    {
        TaskCompletionSource<bool>? pending;
        lock (_lock)
        {
            if (!_pendingReplies.TryDequeue(out pending))
            {
                throw new SshProtocolException("The server answered a channel request nobody made.", DisconnectReason.ProtocolError);
            }
        }

        pending.TrySetResult(success);
    }

    internal bool HandleRequest(string type, ref SshReader reader)
    {
        switch (type)
        {
            case "exit-status":
                ExitCode = unchecked((int)reader.ReadUInt32());
                return true;
            case "exit-signal":
                ExitSignal = ServerText.Sanitize(reader.ReadAsciiString(), 32);
                return true;
            default:
                return false;
        }
    }

    internal bool OnClose()
    {
        _output.Writer.TryComplete();
        _opened.TrySetException(new SshChannelException("The server closed the channel before opening it."));
        FailPendingReplies(new SshChannelException("The channel closed."));
        _closed.TrySetResult();
        return TryMarkCloseSent();
    }

    internal void Fail(Exception exception)
    {
        _output.Writer.TryComplete(exception);
        _opened.TrySetException(exception);
        FailPendingReplies(exception);
        lock (_lock)
        {
            _windowGrown.TrySetException(exception);
        }

        _closed.TrySetException(exception);
        _ = _closed.Task.Exception;
        _ = _opened.Task.Exception;
    }

    private bool TryMarkCloseSent()
    {
        lock (_lock)
        {
            if (_closeSent)
            {
                return false;
            }

            _closeSent = true;
            return true;
        }
    }

    private void FailPendingReplies(Exception exception)
    {
        lock (_lock)
        {
            while (_pendingReplies.TryDequeue(out var pending))
            {
                pending.TrySetException(exception);
            }
        }
    }

    private async Task<int> ReserveWindowAsync(int wanted, CancellationToken cancellationToken)
    {
        while (true)
        {
            Task grown;
            lock (_lock)
            {
                if (_closeSent || _closed.Task.IsCompleted)
                {
                    throw new SshChannelException("The channel is closed.");
                }

                if (_remoteWindow > 0)
                {
                    var count = (int)Math.Min(Math.Min(wanted, _remoteWindow), Math.Min(_remoteMaxPacket, MaxPacket));
                    _remoteWindow -= count;
                    return count;
                }

                grown = _windowGrown.Task;
            }

            await grown.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ConsumeAsync(int count, CancellationToken cancellationToken)
    {
        uint adjust;
        lock (_lock)
        {
            _consumed += count;
            if (_closeSent || _consumed < InitialWindow / 2)
            {
                return;
            }

            adjust = (uint)_consumed;
            _localWindow += _consumed;
            _consumed = 0;
        }

        var message = new SshWriter().WriteByte(MessageNumber.ChannelWindowAdjust).WriteUInt32(RemoteId).WriteUInt32(adjust).ToArray();
        await _send(message, cancellationToken).ConfigureAwait(false);
    }

    private async Task SendRequestAsync(byte[] request, string what, CancellationToken cancellationToken)
    {
        var reply = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_lock)
        {
            _pendingReplies.Enqueue(reply);
        }

        await _send(request, cancellationToken).ConfigureAwait(false);
        if (await reply.Task.WaitAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        throw new SshChannelException($"The server refused {what}.");
    }

    public void Dispose() => _writeGate.Dispose();

    private SshWriter BeginRequest(string type, bool wantReply) =>
        new SshWriter().WriteByte(MessageNumber.ChannelRequest).WriteUInt32(RemoteId).WriteString(type).WriteBoolean(wantReply);

    private byte[] Message(byte number) => new SshWriter().WriteByte(number).WriteUInt32(RemoteId).ToArray();
}
