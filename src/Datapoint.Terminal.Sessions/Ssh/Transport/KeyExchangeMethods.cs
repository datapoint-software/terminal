using Datapoint.Terminal.Security.Cryptography;
using Datapoint.Terminal.Sessions.Ssh.Protocol;

namespace Datapoint.Terminal.Sessions.Ssh.Transport;

public static class KeyExchangeMethods
{
    public static IKeyExchangeMethod Create(string name) => name switch
    {
        AlgorithmNames.MlKem768X25519 => new MlKemHybridExchange(),
        AlgorithmNames.Curve25519 or AlgorithmNames.Curve25519LibSsh => new Curve25519Exchange(),
        AlgorithmNames.EcdhP256 => new EcdhExchange(NistCurve.P256),
        AlgorithmNames.EcdhP384 => new EcdhExchange(NistCurve.P384),
        AlgorithmNames.EcdhP521 => new EcdhExchange(NistCurve.P521),
        _ => throw new SshProtocolException($"Key exchange {name} isn't supported.", DisconnectReason.KeyExchangeFailed),
    };

    internal static byte[] EncodeAsMPInt(ReadOnlySpan<byte> secret) => new SshWriter().WriteMPInt(secret).ToArray();

    internal static byte[] EncodeAsString(ReadOnlySpan<byte> secret) => new SshWriter().WriteString(secret).ToArray();

    private sealed class Curve25519Exchange : IKeyExchangeMethod
    {
        private readonly X25519KeyPair _key = X25519KeyPair.Create();

        public HashKind Hash => HashKind.Sha256;

        public byte[] ClientPublic => _key.PublicKey;

        public bool TryComputeSecret(ReadOnlySpan<byte> serverPublic, out byte[] encodedSecret)
        {
            encodedSecret = [];
            if (!_key.TryAgree(serverPublic, out var secret))
            {
                return false;
            }

            encodedSecret = EncodeAsMPInt(secret);
            SecretMemory.Wipe(secret);
            return true;
        }
    }

    private sealed class EcdhExchange(NistCurve curve) : IKeyExchangeMethod
    {
        private readonly EcdhKeyPair _key = EcdhKeyPair.Create(curve);

        public HashKind Hash => NistCurves.HashOf(curve);

        public byte[] ClientPublic => _key.PublicKey;

        public bool TryComputeSecret(ReadOnlySpan<byte> serverPublic, out byte[] encodedSecret)
        {
            encodedSecret = [];
            if (!_key.TryAgree(serverPublic, out var secret))
            {
                return false;
            }

            encodedSecret = EncodeAsMPInt(secret);
            SecretMemory.Wipe(secret);
            return true;
        }
    }

    private sealed class MlKemHybridExchange : IKeyExchangeMethod
    {
        private readonly MlKem768KeyPair _kem = MlKem768KeyPair.Create();
        private readonly X25519KeyPair _ecdh = X25519KeyPair.Create();

        public HashKind Hash => HashKind.Sha256;

        public byte[] ClientPublic => [.. _kem.PublicKey, .. _ecdh.PublicKey];

        public bool TryComputeSecret(ReadOnlySpan<byte> serverPublic, out byte[] encodedSecret)
        {
            encodedSecret = [];
            if (serverPublic.Length != MlKem768KeyPair.CiphertextSize + X25519KeyPair.KeySize)
            {
                return false;
            }

            if (!_kem.TryDecapsulate(serverPublic[..MlKem768KeyPair.CiphertextSize], out var kemSecret)
                || !_ecdh.TryAgree(serverPublic[MlKem768KeyPair.CiphertextSize..], out var ecdhSecret))
            {
                return false;
            }

            var combined = Hashing.Hash(HashKind.Sha256, [.. kemSecret, .. ecdhSecret]);
            encodedSecret = EncodeAsString(combined);
            SecretMemory.Wipe(kemSecret);
            SecretMemory.Wipe(ecdhSecret);
            SecretMemory.Wipe(combined);
            return true;
        }
    }
}
