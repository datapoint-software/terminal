using Datapoint.Terminal.Security.Cryptography;
using Datapoint.Terminal.Sessions.Ssh.Protocol;
using Datapoint.Terminal.Sessions.Ssh.Transport;

namespace Datapoint.Terminal.Sessions.Ssh.Keys;

public abstract class SshPublicKey(string keyType, byte[] blob)
{
    public string KeyType { get; } = keyType;

    public byte[] Blob { get; } = blob;

    public string Fingerprint => "SHA256:" + Convert.ToBase64String(Hashing.Hash(HashKind.Sha256, Blob)).TrimEnd('=');

    public static SshPublicKey Parse(ReadOnlySpan<byte> blob, int minimumRsaBits = RsaPublicKey.DefaultMinimumBits)
    {
        var reader = new SshReader(blob);
        var keyType = reader.ReadAsciiString();
        return keyType switch
        {
            AlgorithmNames.Ed25519 => Ed25519SshKey.Read(ref reader, blob),
            AlgorithmNames.EcdsaP256 or AlgorithmNames.EcdsaP384 or AlgorithmNames.EcdsaP521 => EcdsaSshKey.Read(keyType, ref reader, blob),
            AlgorithmNames.RsaSha1 => RsaSshKey.Read(ref reader, blob, minimumRsaBits),
            _ => throw new SshProtocolException($"Host key type {keyType} isn't supported.", DisconnectReason.HostKeyNotVerifiable),
        };
    }

    public bool VerifySignature(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signatureBlob, string expectedAlgorithm)
    {
        try
        {
            var reader = new SshReader(signatureBlob);
            var algorithm = reader.ReadAsciiString();
            var signature = reader.ReadString();
            reader.ExpectEnd();
            return string.Equals(algorithm, expectedAlgorithm, StringComparison.Ordinal) && Verify(algorithm, data, signature);
        }
        catch (SshProtocolException)
        {
            return false;
        }
    }

    public bool HasSameBlob(SshPublicKey? other) => other is not null && Blob.AsSpan().SequenceEqual(other.Blob);

    protected abstract bool Verify(string algorithm, ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature);
}
