using Datapoint.Terminal.Sessions.Ssh.Authentication;

namespace Datapoint.Terminal.Sessions.Ssh.Agent;

public sealed class SshAgentSigner : ISshSigner
{
    private readonly byte[] _blob;

    internal SshAgentSigner(SshAgentClient agent, string keyType, byte[] blob, string comment)
    {
        Agent = agent;
        KeyType = keyType;
        _blob = blob;
        Comment = comment;
    }

    public string KeyType { get; }

    public ReadOnlyMemory<byte> PublicKeyBlob => _blob;

    public string Comment { get; }

    internal SshAgentClient Agent { get; }

    public Task<byte[]> SignAsync(ReadOnlyMemory<byte> data, string algorithm, CancellationToken cancellationToken) =>
        Agent.SignAsync(_blob, data, algorithm, cancellationToken);
}
