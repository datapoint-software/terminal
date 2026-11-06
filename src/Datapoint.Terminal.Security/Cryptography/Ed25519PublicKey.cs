using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace Datapoint.Terminal.Security.Cryptography;

public sealed class Ed25519PublicKey
{
    public const int KeySize = 32;
    public const int SignatureSize = 64;

    public static readonly AlgorithmInfo Info = new("Ed25519", CryptographySource.BouncyCastle, IsFipsApproved: false);

    private readonly Ed25519PublicKeyParameters _key;

    private Ed25519PublicKey(Ed25519PublicKeyParameters key) => _key = key;

    public static bool TryCreate(ReadOnlySpan<byte> encoded, out Ed25519PublicKey? key)
    {
        key = null;
        if (encoded.Length != KeySize)
        {
            return false;
        }

        key = new Ed25519PublicKey(new Ed25519PublicKeyParameters(encoded));
        return true;
    }

    public bool Verify(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature)
    {
        if (signature.Length != SignatureSize)
        {
            return false;
        }

        var verifier = new Ed25519Signer();
        verifier.Init(forSigning: false, _key);
        verifier.BlockUpdate(data);
        return verifier.VerifySignature(signature.ToArray());
    }
}
