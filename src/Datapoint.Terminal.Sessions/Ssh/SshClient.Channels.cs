using Datapoint.Terminal.Sessions.Ssh.Channels;
using Datapoint.Terminal.Sessions.Ssh.Protocol;

namespace Datapoint.Terminal.Sessions.Ssh;

public sealed partial class SshClient
{
    private const int MaxChannels = 64;
    private const uint OpenAdministrativelyProhibited = 1;

    private readonly Lock _channelLock = new();
    private readonly Dictionary<uint, SshChannel> _channels = [];
    private uint _nextChannelId;

    public async Task<SshChannel> OpenSessionAsync(CancellationToken cancellationToken)
    {
        ThrowIfClosed();
        if (!_isAuthenticated)
        {
            throw new InvalidOperationException("Log in before opening a channel.");
        }

        var channel = RegisterChannel();
        try
        {
            var open = new SshWriter()
                .WriteByte(MessageNumber.ChannelOpen)
                .WriteString("session")
                .WriteUInt32(channel.LocalId)
                .WriteUInt32(SshChannel.InitialWindow)
                .WriteUInt32(SshChannel.MaxPacket)
                .ToArray();
            await SendAsync(open, cancellationToken).ConfigureAwait(false);
            await channel.WaitUntilOpenAsync(cancellationToken).ConfigureAwait(false);
            return channel;
        }
        catch
        {
            RemoveChannel(channel.LocalId);
            throw;
        }
    }

    private SshChannel RegisterChannel()
    {
        lock (_channelLock)
        {
            if (_channels.Count >= MaxChannels)
            {
                throw new SshChannelException("Too many channels are open on this connection.");
            }

            var channel = new SshChannel(_nextChannelId++, SendAsync);
            _channels.Add(channel.LocalId, channel);
            return channel;
        }
    }

    private void RemoveChannel(uint localId)
    {
        lock (_channelLock)
        {
            _channels.Remove(localId);
        }
    }

    private SshChannel FindChannel(uint localId)
    {
        lock (_channelLock)
        {
            return _channels.TryGetValue(localId, out var channel)
                ? channel
                : throw new SshProtocolException($"The server sent a message for unknown channel {localId}.", DisconnectReason.ProtocolError);
        }
    }

    private void FailAllChannels(Exception exception)
    {
        SshChannel[] channels;
        lock (_channelLock)
        {
            channels = [.. _channels.Values];
            _channels.Clear();
        }

        foreach (var channel in channels)
        {
            channel.Fail(exception);
        }
    }

    private async Task HandleConnectionMessageAsync(byte[] payload, CancellationToken cancellationToken)
    {
        if (!_isAuthenticated)
        {
            throw new SshProtocolException($"Message {payload[0]} arrived before login.", DisconnectReason.ProtocolError);
        }

        var reader = new SshReader(payload);
        var number = reader.ReadByte();
        switch (number)
        {
            case MessageNumber.GlobalRequest:
                await AnswerGlobalRequestAsync(payload, cancellationToken).ConfigureAwait(false);
                return;
            case MessageNumber.ChannelOpen:
                await RefuseChannelOpenAsync(payload, cancellationToken).ConfigureAwait(false);
                return;
            case MessageNumber.RequestSuccess or MessageNumber.RequestFailure:
                throw new SshProtocolException("The server answered a global request we never made.", DisconnectReason.ProtocolError);
            default:
                await HandleChannelMessageAsync(payload, cancellationToken).ConfigureAwait(false);
                return;
        }
    }

    private async Task HandleChannelMessageAsync(byte[] payload, CancellationToken cancellationToken)
    {
        var reader = new SshReader(payload);
        var number = reader.ReadByte();
        var channel = FindChannel(reader.ReadUInt32());
        if (HandleChannelStateMessage(number, channel, ref reader))
        {
            return;
        }

        switch (number)
        {
            case MessageNumber.ChannelData:
                channel.OnData(reader.ReadString(), isError: false);
                return;
            case MessageNumber.ChannelExtendedData:
                var dataType = reader.ReadUInt32();
                channel.OnData(reader.ReadString(), isError: dataType == 1);
                return;
            case MessageNumber.ChannelEof:
                channel.OnEof();
                return;
            case MessageNumber.ChannelClose:
                await CloseChannelAsync(channel, cancellationToken).ConfigureAwait(false);
                return;
            case MessageNumber.ChannelRequest:
                await AnswerChannelRequestAsync(channel, payload, cancellationToken).ConfigureAwait(false);
                return;
            case MessageNumber.ChannelSuccess or MessageNumber.ChannelFailure:
                channel.OnReply(number == MessageNumber.ChannelSuccess);
                return;
            default:
                throw new SshProtocolException($"Unexpected connection message {number}.", DisconnectReason.ProtocolError);
        }
    }

    private bool HandleChannelStateMessage(byte number, SshChannel channel, ref SshReader reader)
    {
        switch (number)
        {
            case MessageNumber.ChannelOpenConfirmation:
                channel.OnOpenConfirmation(ref reader);
                return true;
            case MessageNumber.ChannelOpenFailure:
                channel.OnOpenFailure(ref reader);
                RemoveChannel(channel.LocalId);
                return true;
            case MessageNumber.ChannelWindowAdjust:
                channel.OnWindowAdjust(ref reader);
                return true;
            default:
                return false;
        }
    }

    private async Task CloseChannelAsync(SshChannel channel, CancellationToken cancellationToken)
    {
        var mustReply = channel.OnClose();
        RemoveChannel(channel.LocalId);
        if (!mustReply)
        {
            return;
        }

        var close = new SshWriter().WriteByte(MessageNumber.ChannelClose).WriteUInt32(channel.RemoteId).ToArray();
        await SendFromReadLoopAsync(close, cancellationToken).ConfigureAwait(false);
    }

    private async Task AnswerChannelRequestAsync(SshChannel channel, byte[] payload, CancellationToken cancellationToken)
    {
        var reader = new SshReader(payload);
        reader.ReadByte();
        reader.ReadUInt32();
        var type = reader.ReadAsciiString();
        var wantReply = reader.ReadBoolean();
        var handled = channel.HandleRequest(type, ref reader);
        if (!wantReply)
        {
            return;
        }

        var reply = new SshWriter().WriteByte(handled ? MessageNumber.ChannelSuccess : MessageNumber.ChannelFailure).WriteUInt32(channel.RemoteId).ToArray();
        await SendFromReadLoopAsync(reply, cancellationToken).ConfigureAwait(false);
    }

    // Refuses every global request, including hostkeys-00@openssh.com: host keys change only when the user accepts them.
    private async Task AnswerGlobalRequestAsync(byte[] payload, CancellationToken cancellationToken)
    {
        var reader = new SshReader(payload);
        reader.ReadByte();
        reader.ReadString();
        if (!reader.ReadBoolean())
        {
            return;
        }

        await SendFromReadLoopAsync([MessageNumber.RequestFailure], cancellationToken).ConfigureAwait(false);
    }

    // We forward no agent, X11 or ports, so channels the server opens are refused.
    private async Task RefuseChannelOpenAsync(byte[] payload, CancellationToken cancellationToken)
    {
        var reader = new SshReader(payload);
        reader.ReadByte();
        reader.ReadString();
        var senderChannel = reader.ReadUInt32();
        var failure = new SshWriter()
            .WriteByte(MessageNumber.ChannelOpenFailure)
            .WriteUInt32(senderChannel)
            .WriteUInt32(OpenAdministrativelyProhibited)
            .WriteString("Not allowed by the client.")
            .WriteString(string.Empty)
            .ToArray();
        await SendFromReadLoopAsync(failure, cancellationToken).ConfigureAwait(false);
    }
}
