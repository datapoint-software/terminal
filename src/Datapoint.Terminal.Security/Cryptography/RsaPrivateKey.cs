using System.Security.Cryptography;

namespace Datapoint.Terminal.Security.Cryptography;

public sealed class RsaPrivateKey : IDisposable
{
    private readonly RSA _key;

    private RsaPrivateKey(RSA key) => _key = key;

    public static RsaPrivateKey Create(int bits) => new(RSA.Create(bits));

    public static RsaPrivateKey FromParameters(RsaKeyParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var rsa = RSA.Create();
        rsa.ImportParameters(new RSAParameters
        {
            Modulus = parameters.Modulus,
            Exponent = parameters.PublicExponent,
            D = parameters.PrivateExponent,
            P = parameters.P,
            Q = parameters.Q,
            DP = parameters.DP,
            DQ = parameters.DQ,
            InverseQ = parameters.InverseQ,
        });
        return new RsaPrivateKey(rsa);
    }

    public (byte[] Exponent, byte[] Modulus) PublicKey
    {
        get
        {
            var parameters = _key.ExportParameters(includePrivateParameters: false);
            return (parameters.Exponent!, parameters.Modulus!);
        }
    }

    public byte[] Sign(ReadOnlySpan<byte> data, HashKind hash) =>
        _key.SignData(data, Hashing.NameOf(hash), RSASignaturePadding.Pkcs1);

    public void Dispose() => _key.Dispose();
}
