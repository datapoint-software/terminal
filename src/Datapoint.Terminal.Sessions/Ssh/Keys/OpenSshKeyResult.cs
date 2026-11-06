using Datapoint.Terminal.Sessions.Ssh.Authentication;

namespace Datapoint.Terminal.Sessions.Ssh.Keys;

public sealed record OpenSshKeyResult(ISshSigner? Signer, string? Problem, bool IsEncrypted = false)
{
    public string Comment { get; init; } = string.Empty;
}
