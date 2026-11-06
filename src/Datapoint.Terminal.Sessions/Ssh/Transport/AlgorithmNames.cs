namespace Datapoint.Terminal.Sessions.Ssh.Transport;

public static class AlgorithmNames
{
    public const string MlKem768X25519 = "mlkem768x25519-sha256";
    public const string Curve25519 = "curve25519-sha256";
    public const string Curve25519LibSsh = "curve25519-sha256@libssh.org";
    public const string EcdhP256 = "ecdh-sha2-nistp256";
    public const string EcdhP384 = "ecdh-sha2-nistp384";
    public const string EcdhP521 = "ecdh-sha2-nistp521";
    public const string ExtInfoClient = "ext-info-c";
    public const string StrictKexClient = "kex-strict-c";
    public const string StrictKexClientOpenSsh = "kex-strict-c-v00@openssh.com";
    public const string StrictKexServer = "kex-strict-s";
    public const string StrictKexServerOpenSsh = "kex-strict-s-v00@openssh.com";

    public const string Ed25519 = "ssh-ed25519";
    public const string EcdsaP256 = "ecdsa-sha2-nistp256";
    public const string EcdsaP384 = "ecdsa-sha2-nistp384";
    public const string EcdsaP521 = "ecdsa-sha2-nistp521";
    public const string RsaSha512 = "rsa-sha2-512";
    public const string RsaSha256 = "rsa-sha2-256";
    public const string RsaSha1 = "ssh-rsa";

    public const string ChaCha20Poly1305 = "chacha20-poly1305@openssh.com";
    public const string Aes256Gcm = "aes256-gcm@openssh.com";
    public const string Aes128Gcm = "aes128-gcm@openssh.com";
    public const string Aes256Ctr = "aes256-ctr";
    public const string Aes192Ctr = "aes192-ctr";
    public const string Aes128Ctr = "aes128-ctr";

    public const string HmacSha256Etm = "hmac-sha2-256-etm@openssh.com";
    public const string HmacSha512Etm = "hmac-sha2-512-etm@openssh.com";

    public const string NoCompression = "none";
}
