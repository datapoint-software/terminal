namespace Datapoint.Terminal.Sessions.Ssh.Authentication;

/// <summary>A key that can prove the user's identity: a key in memory, or one held by the SSH agent.</summary>
public interface ISshSigner
{
    /// <summary>The key type, e.g. ssh-ed25519 or ssh-rsa.</summary>
    string KeyType { get; }

    /// <summary>The public key in SSH wire format.</summary>
    ReadOnlyMemory<byte> PublicKeyBlob { get; }

    /// <summary>Signs <paramref name="data"/> and returns the signature in SSH wire format (algorithm name and signature).</summary>
    Task<byte[]> SignAsync(ReadOnlyMemory<byte> data, string algorithm, CancellationToken cancellationToken);
}
