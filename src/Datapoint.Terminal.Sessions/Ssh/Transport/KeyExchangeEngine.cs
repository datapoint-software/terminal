using Datapoint.Terminal.Security.Cryptography;
using Datapoint.Terminal.Sessions.Ssh.Keys;
using Datapoint.Terminal.Sessions.Ssh.Protocol;

namespace Datapoint.Terminal.Sessions.Ssh.Transport;

public sealed class KeyExchangeEngine(
    PacketTransport packets,
    SshConnectionOptions options,
    string clientVersion,
    string serverVersion)
{
    private KexInit? _clientInit;
    private KexInit? _serverInit;
    private IKeyExchangeMethod? _method;
    private IPacketProtection? _pendingReceive;
    private Stage _stage = Stage.Idle;

    private enum Stage
    {
        Idle,
        AwaitingServerInit,
        AwaitingReply,
        AwaitingNewKeys,
    }

    public byte[]? SessionId { get; private set; }

    public SshPublicKey? HostKey { get; private set; }

    public NegotiatedAlgorithms? Algorithms { get; private set; }

    public bool IsStrict { get; private set; }

    public bool InProgress => _stage != Stage.Idle;

    public int CompletedExchanges { get; private set; }

    public bool OurInitSent => _clientInit is not null;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_clientInit is not null)
        {
            return;
        }

        _clientInit = KexInit.ForClient(options.Algorithms, isInitial: CompletedExchanges == 0);
        if (_stage == Stage.Idle)
        {
            _stage = Stage.AwaitingServerInit;
        }

        await packets.SendAsync(_clientInit.Payload, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> HandleAsync(byte[] payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        switch (payload[0])
        {
            case MessageNumber.KexInit when _serverInit is null:
                await OnServerInitAsync(payload, cancellationToken).ConfigureAwait(false);
                return false;
            case MessageNumber.KexEcdhReply when _stage == Stage.AwaitingReply:
                await OnReplyAsync(payload, cancellationToken).ConfigureAwait(false);
                return false;
            case MessageNumber.NewKeys when _stage == Stage.AwaitingNewKeys:
                OnNewKeys();
                return true;
            default:
                throw new SshProtocolException($"Unexpected key exchange message {payload[0]}.", DisconnectReason.KeyExchangeFailed);
        }
    }

    private async Task OnServerInitAsync(byte[] payload, CancellationToken cancellationToken)
    {
        _stage = Stage.AwaitingServerInit;
        await StartAsync(cancellationToken).ConfigureAwait(false);
        _serverInit = KexInit.Parse(payload);
        var negotiated = Negotiation.Negotiate(_clientInit!, _serverInit, isInitial: CompletedExchanges == 0);
        if (CompletedExchanges == 0)
        {
            IsStrict = negotiated.IsStrictKex;
        }

        Algorithms = negotiated;
        _method = KeyExchangeMethods.Create(negotiated.KeyExchange);
        var init = new SshWriter().WriteByte(MessageNumber.KexEcdhInit).WriteString(_method.ClientPublic).ToArray();
        _stage = Stage.AwaitingReply;
        await packets.SendAsync(init, cancellationToken).ConfigureAwait(false);
    }

    private async Task OnReplyAsync(byte[] payload, CancellationToken cancellationToken)
    {
        var reader = new SshReader(payload);
        reader.ReadByte();
        var hostKeyBlob = reader.ReadString().ToArray();
        var serverPublic = reader.ReadString().ToArray();
        var signature = reader.ReadString().ToArray();
        reader.ExpectEnd();
        if (!_method!.TryComputeSecret(serverPublic, out var secret))
        {
            throw new SshProtocolException("The server's key exchange value is invalid.", DisconnectReason.KeyExchangeFailed);
        }

        var exchangeHash = ComputeExchangeHash(hostKeyBlob, serverPublic, secret);
        var hostKey = SshPublicKey.Parse(hostKeyBlob, options.MinimumRsaBits);
        if (!hostKey.VerifySignature(exchangeHash, signature, Algorithms!.HostKey))
        {
            throw new SshProtocolException("The server's host key signature is invalid.", DisconnectReason.HostKeyNotVerifiable);
        }

        await AcceptHostKeyAsync(hostKey, cancellationToken).ConfigureAwait(false);
        SessionId ??= exchangeHash;
        await SendNewKeysAsync(secret, exchangeHash, cancellationToken).ConfigureAwait(false);
        SecretMemory.Wipe(secret);
    }

    private async Task AcceptHostKeyAsync(SshPublicKey hostKey, CancellationToken cancellationToken)
    {
        if (HostKey is not null)
        {
            if (!HostKey.HasSameBlob(hostKey))
            {
                throw new SshProtocolException("The server changed its host key while rekeying.", DisconnectReason.HostKeyNotVerifiable);
            }

            return;
        }

        var decision = await options.HostKeyVerifier.VerifyAsync(options.Host, options.Port, hostKey, cancellationToken).ConfigureAwait(false);
        if (decision != HostKeyDecision.Accepted)
        {
            throw new SshProtocolException("The host key wasn't accepted.", DisconnectReason.HostKeyNotVerifiable);
        }

        HostKey = hostKey;
    }

    private byte[] ComputeExchangeHash(byte[] hostKeyBlob, byte[] serverPublic, byte[] encodedSecret)
    {
        var data = new SshWriter()
            .WriteString(clientVersion)
            .WriteString(serverVersion)
            .WriteString(_clientInit!.Payload)
            .WriteString(_serverInit!.Payload)
            .WriteString(hostKeyBlob)
            .WriteString(_method!.ClientPublic)
            .WriteString(serverPublic)
            .WriteRaw(encodedSecret);
        return Hashing.Hash(_method.Hash, data.WrittenSpan);
    }

    private async Task SendNewKeysAsync(byte[] secret, byte[] exchangeHash, CancellationToken cancellationToken)
    {
        var hash = _method!.Hash;
        var sessionId = SessionId!;
        byte[] Derive(char letter, int length) => KeyDerivation.Derive(hash, secret, exchangeHash, letter, sessionId, length);
        var send = ProtectionFactory.Create(Algorithms!.CipherClientToServer, Algorithms.MacClientToServer, Derive, clientToServer: true);
        _pendingReceive = ProtectionFactory.Create(Algorithms.CipherServerToClient, Algorithms.MacServerToClient, Derive, clientToServer: false);
        _stage = Stage.AwaitingNewKeys;
        await packets.SendAsync(new[] { MessageNumber.NewKeys }, cancellationToken).ConfigureAwait(false);
        packets.UseSendProtection(send, resetSequence: IsStrict);
    }

    private void OnNewKeys()
    {
        packets.UseReceiveProtection(_pendingReceive!, resetSequence: IsStrict);
        _pendingReceive = null;
        _clientInit = null;
        _serverInit = null;
        _method = null;
        _stage = Stage.Idle;
        CompletedExchanges++;
    }
}
