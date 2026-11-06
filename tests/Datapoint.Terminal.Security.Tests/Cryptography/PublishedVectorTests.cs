using System.Text;
using Datapoint.Terminal.Security.Cryptography;

namespace Datapoint.Terminal.Security.Tests.Cryptography;

public sealed class PublishedVectorTests
{
    [Fact]
    public void X25519_matches_rfc_7748()
    {
        var alice = X25519KeyPair.FromPrivateKey(Hex.Bytes("77076d0a7318a57d3c16c17251b26645df4c2f87ebc0992ab177fba51db92c2a"));
        var bobPublic = Hex.Bytes("de9edb7d7b7dc1b4d35b61c2ece435373f8343c85b78674dadfc7e146f882b4f");

        var agreed = alice.TryAgree(bobPublic, out var secret);

        Assert.True(agreed);
        Assert.Equal("8520f0098930a754748b7ddcb43ef75a0dbf3a0d26381af4eba4a98eaa9b4e6a", Hex.Of(alice.PublicKey));
        Assert.Equal("4a5d9d5ba4ce2de1728e3bf480350f25e07e21c947d19e3376f09b3c1e161742", Hex.Of(secret));
    }

    [Fact]
    public void Ed25519_matches_rfc_8032_test_1()
    {
        var key = Ed25519PrivateKey.FromSeed(Hex.Bytes("9d61b19deffd5a60ba844af492ec2cc44449c5697b326919703bac031cae7f60"));

        var signature = key.Sign([]);

        Assert.Equal("d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a", Hex.Of(key.PublicKey));
        Assert.Equal("e5564300c360ac729086e2cc806e828a84877f1eb8e5d974d873e065224901555fb8821590a33bacc61e39701cf9b46bd25bf5f0595bbe24655141438e7a100b", Hex.Of(signature));
    }

    [Fact]
    public void ChaCha20_matches_the_all_zero_key_stream()
    {
        var output = new byte[64];

        ChaCha20.Transform(new byte[32], new byte[8], 0, new byte[64], output);

        Assert.Equal("76b8e0ada0f13d90405d6ae55386bd28bdd219b8a08ded1aa836efcc8b770dc7da41597c5157488d7724e03fb8d84a376a43b8f41518a11cc387b669b2ee6586", Hex.Of(output));
    }

    [Fact]
    public void Poly1305_matches_rfc_8439()
    {
        var tag = new byte[16];

        Poly1305Mac.Compute(Hex.Bytes("85d6be7857556d337f4452fe42d506a80103808afb0db2fd4abff6af4149f51b"), Encoding.ASCII.GetBytes("Cryptographic Forum Research Group"), tag);

        Assert.Equal("a8061dc1305136c6c22b8baf0c0127a9", Hex.Of(tag));
    }

    [Fact]
    public void Aes_gcm_matches_the_gcm_specification_test_case_1()
    {
        using var cipher = new AesGcmCipher(new byte[16]);
        var tag = new byte[16];

        cipher.Encrypt(new byte[12], [], [], tag, []);

        Assert.Equal("58e2fccefa7e3061367f1d57a4e7455a", Hex.Of(tag));
    }

    [Fact]
    public void Aes_ctr_matches_nist_sp_800_38a()
    {
        var cipher = new AesCtrCipher(Hex.Bytes("2b7e151628aed2a6abf7158809cf4f3c"), Hex.Bytes("f0f1f2f3f4f5f6f7f8f9fafbfcfdfeff"));
        var output = new byte[16];

        cipher.Transform(Hex.Bytes("6bc1bee22e409f96e93d7e117393172a"), output);

        Assert.Equal("874d6191b620e3261bef6864990db6ce", Hex.Of(output));
    }

    [Fact]
    public void Sha256_matches_fips_180()
    {
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", Hex.Of(Hashing.Hash(HashKind.Sha256, "abc"u8)));
    }
}
