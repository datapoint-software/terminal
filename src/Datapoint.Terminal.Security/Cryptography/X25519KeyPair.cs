using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

namespace Datapoint.Terminal.Security.Cryptography;

public sealed class X25519KeyPair
{
    public const int KeySize = 32;

    public static readonly AlgorithmInfo Info = new("X25519", CryptographySource.BouncyCastle, IsFipsApproved: false);

    private readonly X25519PrivateKeyParameters _privateKey;

    private X25519KeyPair(X25519PrivateKeyParameters privateKey)
    {
        _privateKey = privateKey;
        PublicKey = privateKey.GeneratePublicKey().GetEncoded();
    }

    public byte[] PublicKey { get; }

    public static X25519KeyPair Create() => new(new X25519PrivateKeyParameters(new SecureRandom()));

    public static X25519KeyPair FromPrivateKey(ReadOnlySpan<byte> privateKey) => new(new X25519PrivateKeyParameters(privateKey));

    public bool TryAgree(ReadOnlySpan<byte> peerPublicKey, out byte[] sharedSecret)
    {
        sharedSecret = [];
        if (peerPublicKey.Length != KeySize)
        {
            return false;
        }

        var secret = new byte[KeySize];
        if (!TryGenerateSecret(peerPublicKey, secret) || SecretMemory.IsAllZero(secret))
        {
            return false;
        }

        sharedSecret = secret;
        return true;
    }

    private bool TryGenerateSecret(ReadOnlySpan<byte> peerPublicKey, Span<byte> secret)
    {
        try
        {
            _privateKey.GenerateSecret(new X25519PublicKeyParameters(peerPublicKey), secret);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
