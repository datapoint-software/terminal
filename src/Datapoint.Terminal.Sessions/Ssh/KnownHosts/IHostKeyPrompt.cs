namespace Datapoint.Terminal.Sessions.Ssh.KnownHosts;

// Only the user answers these, in the application's own windows: no approval mode, and nothing in a project or from the
// agent, can accept or change a host key.
public interface IHostKeyPrompt
{
    Task<NewHostKeyChoice> AskAboutNewKeyAsync(HostKeyQuestion question, CancellationToken cancellationToken);

    Task ShowChangedKeyAsync(HostKeyQuestion question, KnownHostResult known, CancellationToken cancellationToken);

    Task ShowRevokedKeyAsync(HostKeyQuestion question, KnownHostResult known, CancellationToken cancellationToken);
}
