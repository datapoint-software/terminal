using Datapoint.Terminal.Security.Cryptography;
using Datapoint.Terminal.Sessions.Ssh.Protocol;

namespace Datapoint.Terminal.Sessions.Ssh.Transport;

public static class ProtectionFactory
{
    public static IPacketProtection Create(string cipher, string? mac, Func<char, int, byte[]> derive, bool clientToServer)
    {
        ArgumentNullException.ThrowIfNull(derive);
        var (ivLetter, keyLetter, macLetter) = clientToServer ? ('A', 'C', 'E') : ('B', 'D', 'F');
        return cipher switch
        {
            AlgorithmNames.ChaCha20Poly1305 => new ChaChaPolyProtection(derive(keyLetter, ChaChaPolyProtection.KeySize)),
            AlgorithmNames.Aes256Gcm => new AesGcmProtection(derive(keyLetter, 32), derive(ivLetter, 12)),
            AlgorithmNames.Aes128Gcm => new AesGcmProtection(derive(keyLetter, 16), derive(ivLetter, 12)),
            AlgorithmNames.Aes256Ctr or AlgorithmNames.Aes192Ctr or AlgorithmNames.Aes128Ctr => CreateCtr(cipher, mac, derive, ivLetter, keyLetter, macLetter),
            _ => throw new SshProtocolException($"Cipher {cipher} isn't supported.", DisconnectReason.KeyExchangeFailed),
        };
    }

    private static AesCtrEtmProtection CreateCtr(string cipher, string? mac, Func<char, int, byte[]> derive, char ivLetter, char keyLetter, char macLetter)
    {
        var keySize = cipher switch
        {
            AlgorithmNames.Aes256Ctr => 32,
            AlgorithmNames.Aes192Ctr => 24,
            _ => 16,
        };
        var macHash = mac switch
        {
            AlgorithmNames.HmacSha256Etm => HashKind.Sha256,
            AlgorithmNames.HmacSha512Etm => HashKind.Sha512,
            _ => throw new SshProtocolException($"MAC {mac} isn't supported.", DisconnectReason.KeyExchangeFailed),
        };
        return new AesCtrEtmProtection(derive(keyLetter, keySize), derive(ivLetter, 16), macHash, derive(macLetter, Hashing.SizeOf(macHash)));
    }
}
