using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;

namespace Datapoint.Terminal.Security.Cryptography;

public static class Poly1305Mac
{
    public const int KeySize = 32;
    public const int TagSize = 16;

    public static readonly AlgorithmInfo Info = new("Poly1305", CryptographySource.BouncyCastle, IsFipsApproved: false);

    public static void Compute(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, Span<byte> tag)
    {
        var mac = new Poly1305();
        mac.Init(new KeyParameter(key));
        mac.BlockUpdate(data);
        mac.DoFinal(tag);
    }
}
