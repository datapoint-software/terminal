using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;

namespace Datapoint.Terminal.Security.Cryptography;

public static class ChaCha20
{
    public const int KeySize = 32;
    public const int NonceSize = 8;
    public const int BlockSize = 64;

    public static readonly AlgorithmInfo Info = new("ChaCha20 (original, 64-bit nonce)", CryptographySource.BouncyCastle, IsFipsApproved: false);

    public static void Transform(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, int initialBlock, ReadOnlySpan<byte> input, Span<byte> output)
    {
        var engine = new ChaChaEngine();
        engine.Init(forEncryption: true, new ParametersWithIV(new KeyParameter(key), nonce));
        Span<byte> skipped = stackalloc byte[BlockSize];
        for (var block = 0; block < initialBlock; block++)
        {
            engine.ProcessBytes(skipped, skipped);
        }

        engine.ProcessBytes(input, output);
    }
}
