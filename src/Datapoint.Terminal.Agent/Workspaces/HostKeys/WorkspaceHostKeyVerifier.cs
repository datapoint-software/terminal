using Datapoint.Terminal.Sessions.Ssh;
using Datapoint.Terminal.Sessions.Ssh.Keys;

namespace Datapoint.Terminal.Agent.Workspaces.HostKeys;

// Asks the user's own verifier first: a key it refuses stays refused. A key it accepts that differs from the one this workspace
// last saw means the machine may have been rebuilt or replaced, so the user is asked about this workspace's documentation for it
// (product decisions 64 and 93), even when the key was accepted in another workspace. Nothing in the workspace's shared files
// takes part, so they can never approve a server.
public sealed class WorkspaceHostKeyVerifier(IHostKeyVerifier inner, WorkspaceHostKeys keys, string connection, Func<HostKeyChange, CancellationToken, Task<bool>> askAsync) : IHostKeyVerifier
{
    public async Task<HostKeyDecision> VerifyAsync(string host, int port, SshPublicKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (await inner.VerifyAsync(host, port, key, cancellationToken).ConfigureAwait(false) != HostKeyDecision.Accepted)
        {
            return HostKeyDecision.Rejected;
        }

        var seen = key.KeyType + " " + key.Fingerprint;
        var before = keys.LastSeen(connection);
        if (before is not null && !string.Equals(before, seen, StringComparison.Ordinal)
            && !await askAsync(new HostKeyChange(connection, host, port, before, seen), cancellationToken).ConfigureAwait(false))
        {
            return HostKeyDecision.Rejected;
        }

        try
        {
            keys.Record(connection, seen);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Not being able to remember the key only means the next connection can't compare; it isn't a reason to refuse.
        }

        return HostKeyDecision.Accepted;
    }
}
