using Datapoint.Terminal.Security.Cryptography;

namespace Datapoint.Terminal.Security.Tests.Cryptography;

public sealed class KeyExchangeTests
{
    [Fact]
    public void X25519_refuses_a_low_order_point()
    {
        var key = X25519KeyPair.Create();

        Assert.False(key.TryAgree(new byte[32], out _));
    }

    [Fact]
    public void X25519_refuses_a_key_of_the_wrong_length()
    {
        var key = X25519KeyPair.Create();

        Assert.False(key.TryAgree(new byte[31], out _));
    }

    [Theory]
    [InlineData(NistCurve.P256)]
    [InlineData(NistCurve.P384)]
    [InlineData(NistCurve.P521)]
    public void Ecdh_agrees_on_the_same_secret(NistCurve curve)
    {
        using var client = EcdhKeyPair.Create(curve);
        using var server = EcdhKeyPair.Create(curve);

        Assert.True(client.TryAgree(server.PublicKey, out var clientSecret));
        Assert.True(server.TryAgree(client.PublicKey, out var serverSecret));
        Assert.Equal(clientSecret, serverSecret);
    }

    [Fact]
    public void Ecdh_refuses_a_point_off_the_curve()
    {
        using var client = EcdhKeyPair.Create(NistCurve.P256);
        using var server = EcdhKeyPair.Create(NistCurve.P256);
        var tampered = server.PublicKey.ToArray();
        tampered[^1] ^= 1;

        Assert.False(client.TryAgree(tampered, out _));
    }

    [Fact]
    public void Ecdh_refuses_a_compressed_point()
    {
        using var client = EcdhKeyPair.Create(NistCurve.P256);
        using var server = EcdhKeyPair.Create(NistCurve.P256);
        var compressed = server.PublicKey.ToArray();
        compressed[0] = 0x02;

        Assert.False(client.TryAgree(compressed, out _));
    }

    [Fact]
    public void Ml_kem_768_round_trips_a_shared_secret()
    {
        var client = MlKem768KeyPair.Create();

        var (ciphertext, serverSecret) = MlKem768KeyPair.Encapsulate(client.PublicKey);

        Assert.Equal(MlKem768KeyPair.PublicKeySize, client.PublicKey.Length);
        Assert.Equal(MlKem768KeyPair.CiphertextSize, ciphertext.Length);
        Assert.True(client.TryDecapsulate(ciphertext, out var clientSecret));
        Assert.Equal(serverSecret, clientSecret);
    }

    [Fact]
    public void Ml_kem_768_refuses_a_ciphertext_of_the_wrong_length()
    {
        var client = MlKem768KeyPair.Create();

        Assert.False(client.TryDecapsulate(new byte[100], out _));
    }
}
