using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace Datapoint.Terminal.Security.Cryptography;

public sealed class AesCtrCipher
{
    public const int BlockSize = 16;

    public static readonly AlgorithmInfo Info = new("AES-CTR", CryptographySource.BouncyCastle, IsFipsApproved: false);

    private readonly SicBlockCipher _cipher;

    public AesCtrCipher(ReadOnlySpan<byte> key, ReadOnlySpan<byte> initialCounter)
    {
        _cipher = new SicBlockCipher(new AesEngine());
        _cipher.Init(forEncryption: true, new ParametersWithIV(new KeyParameter(key), initialCounter));
    }

    public void Transform(ReadOnlySpan<byte> input, Span<byte> output)
    {
        if (input.Length % BlockSize != 0 || output.Length < input.Length)
        {
            throw new ArgumentException("AES-CTR transforms whole blocks only.", nameof(input));
        }

        for (var offset = 0; offset < input.Length; offset += BlockSize)
        {
            _cipher.ProcessBlock(input.Slice(offset, BlockSize), output.Slice(offset, BlockSize));
        }
    }
}
