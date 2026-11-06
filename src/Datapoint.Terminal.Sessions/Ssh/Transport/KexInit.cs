using Datapoint.Terminal.Security.Cryptography;
using Datapoint.Terminal.Sessions.Ssh.Protocol;

namespace Datapoint.Terminal.Sessions.Ssh.Transport;

public sealed record KexInit(
    IReadOnlyList<string> KeyExchange,
    IReadOnlyList<string> HostKey,
    IReadOnlyList<string> CipherClientToServer,
    IReadOnlyList<string> CipherServerToClient,
    IReadOnlyList<string> MacClientToServer,
    IReadOnlyList<string> MacServerToClient,
    IReadOnlyList<string> CompressionClientToServer,
    IReadOnlyList<string> CompressionServerToClient,
    bool FirstKexPacketFollows,
    byte[] Payload)
{
    public static KexInit ForClient(AlgorithmPreferences preferences, bool isInitial)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        IReadOnlyList<string> keyExchange = isInitial
            ? [.. preferences.KeyExchange, AlgorithmNames.ExtInfoClient, AlgorithmNames.StrictKexClient, AlgorithmNames.StrictKexClientOpenSsh]
            : preferences.KeyExchange;
        var writer = new SshWriter()
            .WriteByte(MessageNumber.KexInit)
            .WriteRaw(RandomBytes.Create(16))
            .WriteNameList(keyExchange)
            .WriteNameList(preferences.HostKey)
            .WriteNameList(preferences.Cipher)
            .WriteNameList(preferences.Cipher)
            .WriteNameList(preferences.Mac)
            .WriteNameList(preferences.Mac)
            .WriteNameList([AlgorithmNames.NoCompression])
            .WriteNameList([AlgorithmNames.NoCompression])
            .WriteNameList([])
            .WriteNameList([])
            .WriteBoolean(false)
            .WriteUInt32(0);
        return Parse(writer.ToArray());
    }

    public static KexInit Parse(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var reader = new SshReader(payload);
        if (reader.ReadByte() != MessageNumber.KexInit)
        {
            throw new SshProtocolException("Expected a KEXINIT message.", DisconnectReason.KeyExchangeFailed);
        }

        reader.ReadRaw(16);
        var lists = new IReadOnlyList<string>[10];
        for (var index = 0; index < lists.Length; index++)
        {
            lists[index] = reader.ReadNameList();
        }

        var firstFollows = reader.ReadBoolean();
        reader.ReadUInt32();
        return new KexInit(lists[0], lists[1], lists[2], lists[3], lists[4], lists[5], lists[6], lists[7], firstFollows, payload);
    }

    public bool Offers(string name) => KeyExchange.Contains(name, StringComparer.Ordinal);
}
