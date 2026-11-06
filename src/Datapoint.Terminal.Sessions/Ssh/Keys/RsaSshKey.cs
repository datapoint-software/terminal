using Datapoint.Terminal.Security.Cryptography;
using Datapoint.Terminal.Sessions.Ssh.Protocol;
using Datapoint.Terminal.Sessions.Ssh.Transport;

namespace Datapoint.Terminal.Sessions.Ssh.Keys;

public sealed class RsaSshKey : SshPublicKey
{
    private readonly RsaPublicKey _key;

    private RsaSshKey(RsaPublicKey key, byte[] blob)
        : base(AlgorithmNames.RsaSha1, blob) => _key = key;

    public int Bits => _key.Bits;

    internal static RsaSshKey Read(ref SshReader reader, ReadOnlySpan<byte> blob, int minimumBits)
    {
        var exponent = reader.ReadMPIntUnsigned();
        var modulus = reader.ReadMPIntUnsigned();
        reader.ExpectEnd();
        if (!RsaPublicKey.TryCreate(exponent, modulus, minimumBits, out var key))
        {
            throw new SshProtocolException($"The RSA key is shorter than the minimum of {minimumBits} bits.", DisconnectReason.HostKeyNotVerifiable);
        }

        return new RsaSshKey(key!, blob.ToArray());
    }

    protected override bool Verify(string algorithm, ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature)
    {
        var hash = algorithm switch
        {
            AlgorithmNames.RsaSha512 => HashKind.Sha512,
            AlgorithmNames.RsaSha256 => HashKind.Sha256,
            AlgorithmNames.RsaSha1 => HashKind.Sha1,
            _ => (HashKind?)null,
        };
        return hash is { } kind && _key.Verify(data, signature, kind);
    }
}
