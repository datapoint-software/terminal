using Datapoint.Terminal.Security.Cryptography;

namespace Datapoint.Terminal.Sessions.Ssh.Transport;

public interface IKeyExchangeMethod
{
    HashKind Hash { get; }

    byte[] ClientPublic { get; }

    bool TryComputeSecret(ReadOnlySpan<byte> serverPublic, out byte[] encodedSecret);
}
