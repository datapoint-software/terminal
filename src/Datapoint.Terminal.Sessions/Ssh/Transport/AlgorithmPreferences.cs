namespace Datapoint.Terminal.Sessions.Ssh.Transport;

public sealed record AlgorithmPreferences(
    IReadOnlyList<string> KeyExchange,
    IReadOnlyList<string> HostKey,
    IReadOnlyList<string> Cipher,
    IReadOnlyList<string> Mac)
{
    public static AlgorithmPreferences Standard { get; } = new(
        [AlgorithmNames.MlKem768X25519, AlgorithmNames.Curve25519, AlgorithmNames.Curve25519LibSsh, AlgorithmNames.EcdhP256, AlgorithmNames.EcdhP384, AlgorithmNames.EcdhP521],
        [AlgorithmNames.Ed25519, AlgorithmNames.EcdsaP256, AlgorithmNames.EcdsaP384, AlgorithmNames.EcdsaP521, AlgorithmNames.RsaSha512, AlgorithmNames.RsaSha256],
        [AlgorithmNames.ChaCha20Poly1305, AlgorithmNames.Aes256Gcm, AlgorithmNames.Aes128Gcm, AlgorithmNames.Aes256Ctr, AlgorithmNames.Aes192Ctr, AlgorithmNames.Aes128Ctr],
        [AlgorithmNames.HmacSha256Etm, AlgorithmNames.HmacSha512Etm]);

    public static AlgorithmPreferences Fips { get; } = new(
        [AlgorithmNames.EcdhP256, AlgorithmNames.EcdhP384, AlgorithmNames.EcdhP521],
        [AlgorithmNames.EcdsaP256, AlgorithmNames.EcdsaP384, AlgorithmNames.EcdsaP521, AlgorithmNames.RsaSha512, AlgorithmNames.RsaSha256],
        [AlgorithmNames.Aes256Gcm, AlgorithmNames.Aes128Gcm],
        [AlgorithmNames.HmacSha256Etm, AlgorithmNames.HmacSha512Etm]);

    public AlgorithmPreferences WithLegacyRsaSignatures() =>
        this with { HostKey = [.. HostKey, AlgorithmNames.RsaSha1] };
}
