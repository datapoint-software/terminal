using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using Datapoint.Terminal.Sessions.Ssh.Keys;
using Datapoint.Terminal.Sessions.Ssh.Protocol;
using Datapoint.Terminal.Sessions.Ssh.Transport;

namespace Datapoint.Terminal.Sessions.Ssh;

public sealed partial class SshClient : IAsyncDisposable
{
    private readonly SshConnectionOptions _options;
    private readonly Stream _stream;
    private readonly PacketTransport _packets;
    private readonly KeyExchangeEngine _keyExchange;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly TimeProvider _time;
    private readonly List<byte[]> _deferredWhileExchanging = [];
    private TaskCompletionSource _keysReady = CompletedSource();
    private DateTimeOffset _lastKeyExchange;
    private Task? _readLoop;
    private Exception? _failure;

    private SshClient(SshConnectionOptions options, Stream stream, string serverVersion, TimeProvider time)
    {
        _options = options;
        _stream = stream;
        _time = time;
        ServerVersion = ServerText.Sanitize(serverVersion, 255);
        _packets = new PacketTransport(stream);
        _keyExchange = new KeyExchangeEngine(_packets, options, VersionExchange.ClientVersion, serverVersion);
    }

    public string ServerVersion { get; }

    public SshPublicKey HostKey => _keyExchange.HostKey!;

    public NegotiatedAlgorithms Algorithms => _keyExchange.Algorithms!;

    public bool IsStrictKeyExchange => _keyExchange.IsStrict;

    public int CompletedKeyExchanges => _keyExchange.CompletedExchanges;

    public bool IsConnected => !_lifetime.IsCancellationRequested;

    public string? DisconnectMessage { get; private set; }

    public static async Task<SshClient> ConnectAsync(SshConnectionOptions options, CancellationToken cancellationToken, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        var socket = new TcpClient { NoDelay = true };
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(options.ConnectTimeout);
            await socket.ConnectAsync(options.Host, options.Port, timeout.Token).ConfigureAwait(false);
            return await ConnectAsync(options, socket.GetStream(), cancellationToken, time).ConfigureAwait(false);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    public static async Task<SshClient> ConnectAsync(SshConnectionOptions options, Stream stream, CancellationToken cancellationToken, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(stream);
        var serverVersion = await VersionExchange.RunAsync(stream, cancellationToken).ConfigureAwait(false);
        var client = new SshClient(options, stream, serverVersion, time ?? TimeProvider.System);
        try
        {
            await client.RunInitialKeyExchangeAsync(cancellationToken).ConfigureAwait(false);
            client._readLoop = Task.Run(() => client.ReadLoopAsync(client._lifetime.Token), CancellationToken.None);
            return client;
        }
        catch (SshProtocolException exception)
        {
            await client.TrySendDisconnectAsync(exception.Reason, exception.Message).ConfigureAwait(false);
            await client.DisposeAsync().ConfigureAwait(false);
            throw;
        }
        catch
        {
            await client.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync(DisconnectReason.ByApplication, "Closed by the user.").ConfigureAwait(false);
        await WaitForReadLoopAsync().ConfigureAwait(false);

        await _stream.DisposeAsync().ConfigureAwait(false);
        _packets.Dispose();
        _sendGate.Dispose();
        _lifetime.Dispose();
    }

    [SuppressMessage("Usage", "VSTHRD003", Justification = "The read loop is ours and runs on the thread pool.")]
    private Task WaitForReadLoopAsync() => _readLoop ?? Task.CompletedTask;

    public async Task CloseAsync(DisconnectReason reason, string description)
    {
        if (_lifetime.IsCancellationRequested)
        {
            return;
        }

        await TrySendDisconnectAsync(reason, description).ConfigureAwait(false);
        Fail(new SshDisconnectedException(reason, description));
        _stream.Close();
    }

    internal async Task SendAsync(byte[] payload, CancellationToken cancellationToken)
    {
        var sent = false;
        while (!sent)
        {
            ThrowIfClosed();
            await _keysReady.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_keysReady.Task.IsCompletedSuccessfully)
                {
                    await _packets.SendAsync(payload, cancellationToken).ConfigureAwait(false);
                    sent = true;
                }
            }
            finally
            {
                _sendGate.Release();
            }
        }

        await StartRekeyIfDueAsync(cancellationToken).ConfigureAwait(false);
    }

    // Queued during a key exchange, because the read loop can't wait for an exchange that only it can finish.
    private async Task SendFromReadLoopAsync(byte[] payload, CancellationToken cancellationToken)
    {
        await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_keysReady.Task.IsCompleted)
            {
                _deferredWhileExchanging.Add(payload);
                return;
            }

            await _packets.SendAsync(payload, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendGate.Release();
        }
    }

    private void ThrowIfClosed()
    {
        if (!_lifetime.IsCancellationRequested)
        {
            return;
        }

        throw new SshDisconnectedException(DisconnectMessage ?? "The connection is closed.", _failure ?? new ObjectDisposedException(nameof(SshClient)));
    }

    private async Task RunInitialKeyExchangeAsync(CancellationToken cancellationToken)
    {
        await _keyExchange.StartAsync(cancellationToken).ConfigureAwait(false);
        var isFirst = true;
        var done = false;
        while (!done)
        {
            var payload = await _packets.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            EnsureAllowedDuringInitialExchange(payload[0], isFirst);
            isFirst = false;
            done = await HandleDuringInitialExchangeAsync(payload, cancellationToken).ConfigureAwait(false);
        }

        _lastKeyExchange = _time.GetUtcNow();
    }

    private void EnsureAllowedDuringInitialExchange(byte number, bool isFirst)
    {
        var isTolerated = !_keyExchange.IsStrict && number is MessageNumber.Ignore or MessageNumber.Debug;
        var isAllowed = isFirst
            ? number is MessageNumber.KexInit or MessageNumber.Disconnect
            : MessageNumber.IsKeyExchange(number) || isTolerated || number == MessageNumber.Disconnect;
        if (isAllowed)
        {
            return;
        }

        throw new SshProtocolException($"Message {number} isn't allowed during the first key exchange.", DisconnectReason.ProtocolError);
    }

    private async Task<bool> HandleDuringInitialExchangeAsync(byte[] payload, CancellationToken cancellationToken)
    {
        switch (payload[0])
        {
            case MessageNumber.Disconnect:
                throw ReadDisconnect(payload);
            case MessageNumber.Ignore or MessageNumber.Debug:
                return false;
            default:
                return await _keyExchange.HandleAsync(payload, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var payload = await _packets.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                await DispatchAsync(payload, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Closed by us.
        }
        catch (SshProtocolException exception)
        {
            await TrySendDisconnectAsync(exception.Reason, exception.Message).ConfigureAwait(false);
            Fail(exception);
        }
        catch (SshDisconnectedException exception)
        {
            Fail(exception);
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            Fail(new SshDisconnectedException(cancellationToken.IsCancellationRequested ? "The connection is closed." : "The connection was lost.", exception));
        }
    }

    private async Task DispatchAsync(byte[] payload, CancellationToken cancellationToken)
    {
        var number = payload[0];
        if (MessageNumber.IsKeyExchange(number))
        {
            await HandleKeyExchangeMessageAsync(payload, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (MessageNumber.IsAuthentication(number) || number is MessageNumber.ServiceAccept or MessageNumber.ExtInfo)
        {
            HandleAuthenticationMessage(payload);
            return;
        }

        if (MessageNumber.IsConnection(number))
        {
            await HandleConnectionMessageAsync(payload, cancellationToken).ConfigureAwait(false);
            return;
        }

        await HandleTransportMessageAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    private async Task HandleTransportMessageAsync(byte[] payload, CancellationToken cancellationToken)
    {
        switch (payload[0])
        {
            case MessageNumber.Disconnect:
                throw ReadDisconnect(payload);
            case MessageNumber.Ignore or MessageNumber.Debug or MessageNumber.Unimplemented:
                return;
            default:
                var unimplemented = new SshWriter().WriteByte(MessageNumber.Unimplemented).WriteUInt32(unchecked(_packets.ReceiveSequence - 1)).ToArray();
                await SendFromReadLoopAsync(unimplemented, cancellationToken).ConfigureAwait(false);
                return;
        }
    }

    private async Task HandleKeyExchangeMessageAsync(byte[] payload, CancellationToken cancellationToken)
    {
        if (payload[0] == MessageNumber.KexInit)
        {
            await BeginKeyExchangeAsync(cancellationToken).ConfigureAwait(false);
        }

        var done = await _keyExchange.HandleAsync(payload, cancellationToken).ConfigureAwait(false);
        if (!done)
        {
            return;
        }

        await FinishKeyExchangeAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task FinishKeyExchangeAsync(CancellationToken cancellationToken)
    {
        await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var deferred in _deferredWhileExchanging)
            {
                await _packets.SendAsync(deferred, cancellationToken).ConfigureAwait(false);
            }

            _deferredWhileExchanging.Clear();
            _lastKeyExchange = _time.GetUtcNow();
            _keysReady.TrySetResult();
        }
        finally
        {
            _sendGate.Release();
        }
    }

    private async Task StartRekeyIfDueAsync(CancellationToken cancellationToken)
    {
        if (_keyExchange.InProgress || !IsRekeyDue())
        {
            return;
        }

        await BeginKeyExchangeAsync(cancellationToken).ConfigureAwait(false);
    }

    private bool IsRekeyDue()
    {
        var bytes = _packets.BytesSentWithCurrentKeys + _packets.BytesReceivedWithCurrentKeys;
        var packets = Math.Max(_packets.PacketsSentWithCurrentKeys, _packets.PacketsReceivedWithCurrentKeys);
        var age = _time.GetUtcNow() - _lastKeyExchange;
        return bytes >= _options.RekeyBytes || packets >= _options.RekeyPackets || age >= _options.RekeyInterval;
    }

    private async Task BeginKeyExchangeAsync(CancellationToken cancellationToken)
    {
        await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_keyExchange.OurInitSent)
            {
                return;
            }

            _keysReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await _keyExchange.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendGate.Release();
        }
    }

    private void Fail(Exception exception)
    {
        _failure ??= exception;
        DisconnectMessage ??= exception.Message;
        if (!_lifetime.IsCancellationRequested)
        {
            _lifetime.Cancel();
        }

        _keysReady.TrySetException(_failure);
        _authenticationMessages.Writer.TryComplete(_failure);
        FailAllChannels(_failure);
    }

    private static SshDisconnectedException ReadDisconnect(byte[] payload)
    {
        var reader = new SshReader(payload);
        reader.ReadByte();
        var reason = (DisconnectReason)reader.ReadUInt32();
        var description = ServerText.Sanitize(reader.ReadUtf8String(), 1024);
        return new SshDisconnectedException(reason, $"The server disconnected: {description}");
    }

    private async Task TrySendDisconnectAsync(DisconnectReason reason, string description)
    {
        try
        {
            var payload = new SshWriter().WriteByte(MessageNumber.Disconnect).WriteUInt32((uint)reason).WriteString(description).WriteString(string.Empty).ToArray();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await _packets.SendAsync(payload, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or OperationCanceledException or ObjectDisposedException or SshProtocolException)
        {
            DisconnectMessage ??= exception.Message;
        }
    }

    private static TaskCompletionSource CompletedSource()
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        source.SetResult();
        return source;
    }
}
