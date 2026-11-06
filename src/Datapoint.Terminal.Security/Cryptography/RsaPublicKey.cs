using System.Security.Cryptography;

namespace Datapoint.Terminal.Security.Cryptography;

public sealed class RsaPublicKey : IDisposable
{
    public const int DefaultMinimumBits = 2048;
    public const int AbsoluteMinimumBits = 1024;

    public static readonly AlgorithmInfo Info = new("RSA", CryptographySource.DotNet, IsFipsApproved: true);

    private readonly RSA _key;

    private RsaPublicKey(RSA key, int bits)
    {
        _key = key;
        Bits = bits;
    }

    public int Bits { get; }

    public static bool TryCreate(ReadOnlySpan<byte> exponent, ReadOnlySpan<byte> modulus, int minimumBits, out RsaPublicKey? key)
    {
        key = null;
        var trimmedModulus = modulus.TrimStart((byte)0);
        var trimmedExponent = exponent.TrimStart((byte)0);
        var bits = BitLength(trimmedModulus);
        if (bits < Math.Max(minimumBits, AbsoluteMinimumBits) || bits > 16384 || trimmedExponent.IsEmpty)
        {
            return false;
        }

        var rsa = RSA.Create();
        rsa.ImportParameters(new RSAParameters { Exponent = trimmedExponent.ToArray(), Modulus = trimmedModulus.ToArray() });
        key = new RsaPublicKey(rsa, bits);
        return true;
    }

    public bool Verify(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature, HashKind hash)
    {
        try
        {
            return _key.VerifyData(data, signature, Hashing.NameOf(hash), RSASignaturePadding.Pkcs1);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    public void Dispose() => _key.Dispose();

    private static int BitLength(ReadOnlySpan<byte> value) =>
        value.IsEmpty ? 0 : ((value.Length - 1) * 8) + (32 - int.LeadingZeroCount(value[0]));
}
