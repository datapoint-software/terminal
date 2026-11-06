using Datapoint.Terminal.Sessions.Ssh.Protocol;

namespace Datapoint.Terminal.Sessions.Ssh.Transport;

public static class Negotiation
{
    public static NegotiatedAlgorithms Negotiate(KexInit client, KexInit server, bool isInitial)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(server);
        var cipherOut = Choose("cipher (client to server)", client.CipherClientToServer, server.CipherClientToServer);
        var cipherIn = Choose("cipher (server to client)", client.CipherServerToClient, server.CipherServerToClient);
        Choose("compression (client to server)", client.CompressionClientToServer, server.CompressionClientToServer);
        Choose("compression (server to client)", client.CompressionServerToClient, server.CompressionServerToClient);
        var isStrict = isInitial && (server.Offers(AlgorithmNames.StrictKexServer) || server.Offers(AlgorithmNames.StrictKexServerOpenSsh));
        return new NegotiatedAlgorithms(
            Choose("key exchange", client.KeyExchange.Where(IsRealKeyExchange).ToList(), server.KeyExchange),
            Choose("host key", client.HostKey, server.HostKey),
            cipherOut,
            cipherIn,
            IsAead(cipherOut) ? null : Choose("MAC (client to server)", client.MacClientToServer, server.MacClientToServer),
            IsAead(cipherIn) ? null : Choose("MAC (server to client)", client.MacServerToClient, server.MacServerToClient),
            isStrict,
            server.Offers("ext-info-s"));
    }

    public static bool IsAead(string cipher) =>
        cipher is AlgorithmNames.ChaCha20Poly1305 or AlgorithmNames.Aes256Gcm or AlgorithmNames.Aes128Gcm;

    private static bool IsRealKeyExchange(string name) =>
        name is not (AlgorithmNames.ExtInfoClient or AlgorithmNames.StrictKexClient or AlgorithmNames.StrictKexClientOpenSsh);

    private static string Choose(string category, IReadOnlyList<string> client, IReadOnlyList<string> server)
    {
        var chosen = client.FirstOrDefault(name => server.Contains(name, StringComparer.Ordinal));
        return chosen ?? throw new SshProtocolException(
            $"No {category} algorithm in common. The server offers: {string.Join(", ", server)}. We offer: {string.Join(", ", client)}.",
            DisconnectReason.KeyExchangeFailed);
    }
}
