namespace Datapoint.Terminal.Sessions.Ssh.Transport;

public sealed record NegotiatedAlgorithms(
    string KeyExchange,
    string HostKey,
    string CipherClientToServer,
    string CipherServerToClient,
    string? MacClientToServer,
    string? MacServerToClient,
    bool IsStrictKex,
    bool ServerSupportsExtInfo);
