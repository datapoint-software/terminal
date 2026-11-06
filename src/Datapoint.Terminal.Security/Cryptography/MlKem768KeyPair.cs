using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Kems;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

namespace Datapoint.Terminal.Security.Cryptography;

public sealed class MlKem768KeyPair
{
    public const int PublicKeySize = 1184;
    public const int CiphertextSize = 1088;
    public const int SharedSecretSize = 32;

    public static readonly AlgorithmInfo Info = new("ML-KEM-768", CryptographySource.BouncyCastle, IsFipsApproved: false);

    private readonly MLKemPrivateKeyParameters _privateKey;

    private MlKem768KeyPair(MLKemPrivateKeyParameters privateKey)
    {
        _privateKey = privateKey;
        PublicKey = privateKey.GetPublicKeyEncoded();
    }

    public byte[] PublicKey { get; }

    public static MlKem768KeyPair Create()
    {
        var generator = new MLKemKeyPairGenerator();
        generator.Init(new MLKemKeyGenerationParameters(new SecureRandom(), MLKemParameters.ml_kem_768));
        return new MlKem768KeyPair((MLKemPrivateKeyParameters)generator.GenerateKeyPair().Private);
    }

    public bool TryDecapsulate(ReadOnlySpan<byte> ciphertext, out byte[] sharedSecret)
    {
        sharedSecret = [];
        if (ciphertext.Length != CiphertextSize)
        {
            return false;
        }

        var decapsulator = new MLKemDecapsulator(MLKemParameters.ml_kem_768);
        decapsulator.Init(_privateKey);
        sharedSecret = new byte[SharedSecretSize];
        decapsulator.Decapsulate(ciphertext, sharedSecret);
        return true;
    }

    public static (byte[] Ciphertext, byte[] SharedSecret) Encapsulate(ReadOnlySpan<byte> publicKey)
    {
        var encapsulator = new MLKemEncapsulator(MLKemParameters.ml_kem_768);
        encapsulator.Init(MLKemPublicKeyParameters.FromEncoding(MLKemParameters.ml_kem_768, publicKey.ToArray()));
        var ciphertext = new byte[CiphertextSize];
        var secret = new byte[SharedSecretSize];
        encapsulator.Encapsulate(ciphertext, secret);
        return (ciphertext, secret);
    }
}
