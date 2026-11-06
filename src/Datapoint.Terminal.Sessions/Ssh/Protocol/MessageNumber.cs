namespace Datapoint.Terminal.Sessions.Ssh.Protocol;

public static class MessageNumber
{
    public const byte Disconnect = 1;
    public const byte Ignore = 2;
    public const byte Unimplemented = 3;
    public const byte Debug = 4;
    public const byte ServiceRequest = 5;
    public const byte ServiceAccept = 6;
    public const byte ExtInfo = 7;
    public const byte KexInit = 20;
    public const byte NewKeys = 21;
    public const byte KexEcdhInit = 30;
    public const byte KexEcdhReply = 31;
    public const byte UserAuthRequest = 50;
    public const byte UserAuthFailure = 51;
    public const byte UserAuthSuccess = 52;
    public const byte UserAuthBanner = 53;
    public const byte UserAuthPublicKeyOk = 60;
    public const byte UserAuthPasswordChangeRequest = 60;
    public const byte UserAuthInfoRequest = 60;
    public const byte UserAuthInfoResponse = 61;
    public const byte GlobalRequest = 80;
    public const byte RequestSuccess = 81;
    public const byte RequestFailure = 82;
    public const byte ChannelOpen = 90;
    public const byte ChannelOpenConfirmation = 91;
    public const byte ChannelOpenFailure = 92;
    public const byte ChannelWindowAdjust = 93;
    public const byte ChannelData = 94;
    public const byte ChannelExtendedData = 95;
    public const byte ChannelEof = 96;
    public const byte ChannelClose = 97;
    public const byte ChannelRequest = 98;
    public const byte ChannelSuccess = 99;
    public const byte ChannelFailure = 100;

    public static bool IsTransportGeneric(byte number) => number is >= 1 and <= 4;

    public static bool IsKeyExchange(byte number) => number is >= 20 and <= 49;

    public static bool IsAuthentication(byte number) => number is >= 50 and <= 79;

    public static bool IsConnection(byte number) => number is >= 80 and <= 127;
}
