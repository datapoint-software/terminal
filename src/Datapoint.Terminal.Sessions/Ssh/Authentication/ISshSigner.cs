namespace Datapoint.Terminal.Sessions.Ssh.Authentication;

public interface ISshSigner
{
    string KeyType { get; }

    ReadOnlyMemory<byte> PublicKeyBlob { get; }

    Task<byte[]> SignAsync(ReadOnlyMemory<byte> data, string algorithm, CancellationToken cancellationToken);
}
