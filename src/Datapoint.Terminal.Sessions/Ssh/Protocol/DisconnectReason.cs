namespace Datapoint.Terminal.Sessions.Ssh.Protocol;

public enum DisconnectReason : uint
{
    None = 0,
    HostNotAllowedToConnect = 1,
    ProtocolError = 2,
    KeyExchangeFailed = 3,
    MacError = 5,
    ServiceNotAvailable = 7,
    ProtocolVersionNotSupported = 8,
    HostKeyNotVerifiable = 9,
    ConnectionLost = 10,
    ByApplication = 11,
    AuthCancelledByUser = 13,
    NoMoreAuthMethodsAvailable = 14,
}
