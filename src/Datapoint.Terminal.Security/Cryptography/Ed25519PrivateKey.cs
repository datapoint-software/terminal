using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace Datapoint.Terminal.Security.Cryptography;

public sealed class Ed25519PrivateKey
{
    public const int SeedSize = 32;

    private readonly Ed25519PrivateKeyParameters _key;

    private Ed25519PrivateKey(Ed25519PrivateKeyParameters key)
    {
        _key = key;
        PublicKey = key.GeneratePublicKey().GetEncoded();
    }

    public byte[] PublicKey { get; }

    public static Ed25519PrivateKey FromSeed(ReadOnlySpan<byte> seed)
    {
        if (seed.Length != SeedSize)
        {
            throw new ArgumentException("An Ed25519 seed is 32 bytes.", nameof(seed));
        }

        return new Ed25519PrivateKey(new Ed25519PrivateKeyParameters(seed));
    }

    public byte[] Sign(ReadOnlySpan<byte> data)
    {
        var signer = new Ed25519Signer();
        signer.Init(forSigning: true, _key);
        signer.BlockUpdate(data);
        return signer.GenerateSignature();
    }
}
