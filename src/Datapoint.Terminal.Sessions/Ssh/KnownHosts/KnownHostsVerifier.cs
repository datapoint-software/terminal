using Datapoint.Terminal.Sessions.Ssh.Keys;

namespace Datapoint.Terminal.Sessions.Ssh.KnownHosts;

// A changed key is refused outright, as ssh does: the user must remove the old line themselves, outside the application,
// after finding out why it changed. StrictHostKeyChecking=yes refuses unknown keys without asking.
public sealed class KnownHostsVerifier(KnownHostsFile file, IHostKeyPrompt prompt, string connectionName, string? hostKeyAlias = null, bool isStrict = false) : IHostKeyVerifier
{
    public async Task<HostKeyDecision> VerifyAsync(string host, int port, SshPublicKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        var lookupHost = hostKeyAlias ?? host;
        var lookupPort = hostKeyAlias is null ? port : 22;
        var known = file.Check(lookupHost, lookupPort, key);
        var question = new HostKeyQuestion(connectionName, lookupHost, lookupPort, key.KeyType, key.Fingerprint, known.KnownKeyTypes);
        switch (known.Status)
        {
            case KnownHostStatus.Known:
                return HostKeyDecision.Accepted;
            case KnownHostStatus.Revoked:
                await prompt.ShowRevokedKeyAsync(question, known, cancellationToken).ConfigureAwait(false);
                return HostKeyDecision.Rejected;
            case KnownHostStatus.Changed:
                await prompt.ShowChangedKeyAsync(question, known, cancellationToken).ConfigureAwait(false);
                return HostKeyDecision.Rejected;
            default:
                return isStrict ? HostKeyDecision.Rejected : await AskAsync(question, lookupHost, lookupPort, key, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<HostKeyDecision> AskAsync(HostKeyQuestion question, string host, int port, SshPublicKey key, CancellationToken cancellationToken)
    {
        var choice = await prompt.AskAboutNewKeyAsync(question, cancellationToken).ConfigureAwait(false);
        if (choice == NewHostKeyChoice.TrustAndConnect)
        {
            file.Add(host, port, key);
        }

        return choice == NewHostKeyChoice.Cancel ? HostKeyDecision.Rejected : HostKeyDecision.Accepted;
    }
}
