using System.Security.Cryptography;

namespace Datapoint.Terminal.Security.Cryptography;

public sealed class AesGcmCipher(ReadOnlySpan<byte> key) : IDisposable
{
    public const int NonceSize = 12;
    public const int TagSize = 16;

    public static readonly AlgorithmInfo Info = new("AES-GCM", CryptographySource.DotNet, IsFipsApproved: true);

    private readonly AesGcm _aes = new(key, TagSize);

    public void Encrypt(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> plaintext, Span<byte> ciphertext, Span<byte> tag, ReadOnlySpan<byte> associatedData) =>
        _aes.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);

    public bool TryDecrypt(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> tag, Span<byte> plaintext, ReadOnlySpan<byte> associatedData)
    {
        try
        {
            _aes.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);
            return true;
        }
        catch (AuthenticationTagMismatchException)
        {
            plaintext.Clear();
            return false;
        }
    }

    public void Dispose() => _aes.Dispose();
}
