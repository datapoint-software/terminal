using Datapoint.Terminal.Security.Cryptography;
using Datapoint.Terminal.Sessions.Ssh.Protocol;
using Datapoint.Terminal.Sessions.Ssh.Transport;

namespace Datapoint.Terminal.Sessions.Ssh.Keys;

public sealed class Ed25519SshKey : SshPublicKey
{
    private readonly Ed25519PublicKey _key;

    private Ed25519SshKey(Ed25519PublicKey key, byte[] blob)
        : base(AlgorithmNames.Ed25519, blob) => _key = key;

    internal static Ed25519SshKey Read(ref SshReader reader, ReadOnlySpan<byte> blob)
    {
        var encoded = reader.ReadString();
        reader.ExpectEnd();
        if (!Ed25519PublicKey.TryCreate(encoded, out var key))
        {
            throw new SshProtocolException("An Ed25519 key has the wrong length.", DisconnectReason.HostKeyNotVerifiable);
        }

        return new Ed25519SshKey(key!, blob.ToArray());
    }

    protected override bool Verify(string algorithm, ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature) =>
        algorithm is AlgorithmNames.Ed25519 && _key.Verify(data, signature);
}
