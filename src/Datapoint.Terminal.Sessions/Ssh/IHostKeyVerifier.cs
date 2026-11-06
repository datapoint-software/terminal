using Datapoint.Terminal.Sessions.Ssh.Keys;

namespace Datapoint.Terminal.Sessions.Ssh;

public interface IHostKeyVerifier
{
    Task<HostKeyDecision> VerifyAsync(string host, int port, SshPublicKey key, CancellationToken cancellationToken);
}
