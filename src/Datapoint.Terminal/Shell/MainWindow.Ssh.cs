using System.Net.Sockets;
using Avalonia;
using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Agent.Projects;
using Datapoint.Terminal.Controls;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Sessions;
using Datapoint.Terminal.Sessions.Ssh;
using Datapoint.Terminal.Sessions.Ssh.Authentication;
using Datapoint.Terminal.Sessions.Ssh.Channels;
using Datapoint.Terminal.Sessions.Ssh.Config;
using Datapoint.Terminal.Sessions.Ssh.KnownHosts;
using Datapoint.Terminal.Sessions.Ssh.Protocol;

namespace Datapoint.Terminal.Shell;

public sealed partial class MainWindow
{
    private async Task OpenConnectionAsync(ProjectConnection connection)
    {
        var host = SshConfig.Load().Resolve(connection.SshHost ?? connection.Host!);
        foreach (var problem in host.Problems)
        {
            _chat.AddNotice(problem);
        }

        if (SshLoginPlan.Unsupported(host) is { } unsupported)
        {
            _chat.AddNotice(unsupported);
            return;
        }

        _chat.AddNotice(Strings.Format(Strings.NoticeConnecting, connection.Name));
        try
        {
            var session = await LogInAsync(connection, host).ConfigureAwait(true);
            var view = new TerminalView { Padding = new Thickness(12, 10), IsVisible = false };
            var tab = new TerminalTab(NextTabName(connection.Name), view, CloseTab);
            Attach(tab, new TerminalConnection(session, 80, 24), connection.Environment, DefaultModeFor(connection));
        }
        catch (Exception exception) when (exception is SshAuthenticationException or SshDisconnectedException or SshProtocolException or SshChannelException
            or SocketException or IOException or TimeoutException or OperationCanceledException)
        {
            _chat.AddNotice(Strings.Format(Strings.NoticeConnectFailed, connection.Name, exception.Message));
        }
    }

    private async Task<SshTerminalSession> LogInAsync(ProjectConnection connection, SshHostConfig host)
    {
        var prompts = new SshLoginPrompts(this, connection.Name, _redactor, _chat.AddNotice);
        var knownHosts = new KnownHostsFile([.. host.UserKnownHostsFiles, .. host.GlobalKnownHostsFiles]);
        var verifier = new KnownHostsVerifier(knownHosts, prompts, connection.Name, host.Get("HostKeyAlias"), isStrict: host.IsYes("StrictHostKeyChecking"));
        var options = new SshConnectionOptions
        {
            Host = connection.SshHost is null ? connection.Host! : host.HostName,
            Port = connection.Port ?? host.Port,
            UserName = connection.User ?? host.User,
            HostKeyVerifier = verifier,
        };
        var client = await SshClient.ConnectAsync(options, CancellationToken.None).ConfigureAwait(true);
        var (keys, notes) = await SshLoginPlan.FindKeysAsync(host, ProjectFolders.Home, CancellationToken.None).ConfigureAwait(true);
        try
        {
            foreach (var note in notes)
            {
                _chat.AddNotice(Strings.Format(Strings.NoticeLoginNote, connection.Name, note));
            }

            await client.AuthenticateAsync(keys, prompts, CancellationToken.None).ConfigureAwait(true);
            return await SshTerminalSession.StartAsync(client, "xterm-256color", 80, 24, CancellationToken.None, ownsClient: true).ConfigureAwait(true);
        }
        catch
        {
            await client.DisposeAsync().ConfigureAwait(true);
            throw;
        }
        finally
        {
            foreach (var key in keys.OfType<IDisposable>())
            {
                key.Dispose();
            }
        }
    }

    // Production connections ask before every command unless the project says otherwise (product decision 72).
    private ApprovalMode DefaultModeFor(ProjectConnection connection) =>
        connection.ApprovalMode
        ?? (connection.Environment?.StartsWith("prod", StringComparison.OrdinalIgnoreCase) == true ? ApprovalMode.Ask : _settings.Current.DefaultApprovalMode);
}
