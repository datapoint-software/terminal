using Datapoint.Terminal.Security.Cryptography;

namespace Datapoint.Terminal.Security.Tests.Cryptography;

public sealed class SignatureTests
{
    private static readonly byte[] s_message = "exchange hash"u8.ToArray();

    [Fact]
    public void Ed25519_verifies_its_own_signature_and_rejects_a_tampered_one()
    {
        var key = Ed25519PrivateKey.FromSeed(new byte[32]);
        Assert.True(Ed25519PublicKey.TryCreate(key.PublicKey, out var publicKey));
        var signature = key.Sign(s_message);
        var tampered = signature.ToArray();
        tampered[0] ^= 1;

        Assert.True(publicKey!.Verify(s_message, signature));
        Assert.False(publicKey.Verify(s_message, tampered));
        Assert.False(publicKey.Verify(s_message, signature.AsSpan(0, 63)));
    }

    [Theory]
    [InlineData(NistCurve.P256)]
    [InlineData(NistCurve.P384)]
    [InlineData(NistCurve.P521)]
    public void Ecdsa_verifies_its_own_signature(NistCurve curve)
    {
        using var key = EcdsaPrivateKey.Create(curve);
        Assert.True(EcdsaPublicKey.TryCreate(curve, key.PublicKey, out var publicKey));
        using var verifier = publicKey!;

        Assert.True(verifier.Verify(s_message, key.SignP1363(s_message)));
    }

    [Fact]
    public void Ecdsa_rejects_an_all_zero_signature()
    {
        using var key = EcdsaPrivateKey.Create(NistCurve.P256);
        Assert.True(EcdsaPublicKey.TryCreate(NistCurve.P256, key.PublicKey, out var publicKey));
        using var verifier = publicKey!;

        Assert.False(verifier.Verify(s_message, new byte[64]));
    }

    [Fact]
    public void Rsa_verifies_a_sha2_signature()
    {
        using var key = RsaPrivateKey.Create(2048);
        var (exponent, modulus) = key.PublicKey;
        Assert.True(RsaPublicKey.TryCreate(exponent, modulus, RsaPublicKey.DefaultMinimumBits, out var publicKey));
        using var verifier = publicKey!;

        Assert.True(verifier.Verify(s_message, key.Sign(s_message, HashKind.Sha512), HashKind.Sha512));
        Assert.False(verifier.Verify(s_message, key.Sign(s_message, HashKind.Sha256), HashKind.Sha512));
    }

    [Fact]
    public void Rsa_refuses_keys_shorter_than_the_minimum()
    {
        using var key = RsaPrivateKey.Create(1024);
        var (exponent, modulus) = key.PublicKey;

        Assert.False(RsaPublicKey.TryCreate(exponent, modulus, RsaPublicKey.DefaultMinimumBits, out _));
        Assert.True(RsaPublicKey.TryCreate(exponent, modulus, 1024, out var allowed));
        allowed!.Dispose();
    }
}
