using System.Net.Sockets;
using Avalonia;
using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Agent.Files;
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
    private async Task<TerminalTab?> OpenConnectionAsync(ProjectConnection connection, bool select = true)
    {
        if (connection.IsLocal)
        {
            return await OpenLocalContainerAsync(connection, select).ConfigureAwait(true);
        }

        var config = SshConfig.Load();
        var host = config.Resolve(connection.SshHost ?? connection.Host!);
        foreach (var problem in host.Problems)
        {
            _chat.AddNotice(problem);
        }

        if (JumpsFor(config, host) is not { } jumps)
        {
            return null;
        }

        _chat.AddNotice(Strings.Format(Strings.NoticeConnecting, connection.Name));
        try
        {
            var session = await LogInAsync(connection, config, host, jumps).ConfigureAwait(true);
            if (connection.Container is null && connection.ShellIntegration != false)
            {
                _chat.AddNotice(Strings.Format(session.ShellIntegrationNonce is null ? Strings.NoticeRemoteNoIntegration : Strings.NoticeRemoteIntegration, connection.Name));
            }

            return AttachSsh(connection, host, session, select);
        }
        catch (Exception exception) when (exception is SshAuthenticationException or SshDisconnectedException or SshProtocolException or SshChannelException
            or SocketException or IOException or TimeoutException or OperationCanceledException)
        {
            _chat.AddNotice(Strings.Format(Strings.NoticeConnectFailed, connection.Name, exception.Message));
            return null;
        }
    }

    private TerminalTab AttachSsh(ProjectConnection connection, SshHostConfig host, SshTerminalSession session, bool select)
    {
        var view = new TerminalView { Padding = new Thickness(12, 10), IsVisible = false };
        var tab = new TerminalTab(NextTabName(connection.Name), view, RequestClose) { IsRemote = true, ConnectionName = connection.Name };
        var client = session.Client!;

        // File access and transfer go to the host, not into a container on it, so a container's files are changed through its shell.
        var onHost = connection.Container is null;
        Attach(tab, new TerminalConnection(session, 80, 24), connection.Environment, DefaultModeFor(connection), select, onHost ? new SshHostFiles(client) : null, connection.Container?.Describe());
        tab.Client = onHost ? client : null;
        StartForwards(tab, client, [.. host.GetAll("LocalForward"), .. connection.Forwards ?? []]);
        return tab;
    }

    private async Task<SshTerminalSession> LogInAsync(ProjectConnection connection, SshConfig config, SshHostConfig host, IReadOnlyList<SshJump> jumps)
    {
        SshClient? via = null;
        foreach (var jump in jumps)
        {
            var hop = config.Resolve(jump.Host);
            _chat.AddNotice(Strings.Format(Strings.NoticeJumping, connection.Name, jump.Host));
            via = await LogInClientAsync(jump.Host, hop, hop.HostName, jump.Port ?? hop.Port, jump.User ?? hop.User, via).ConfigureAwait(true);
        }

        var address = connection.SshHost is null ? connection.Host! : host.HostName;
        var client = await LogInClientAsync(connection.Name, host, address, connection.Port ?? host.Port, connection.User ?? host.User, via).ConfigureAwait(true);
        try
        {
            var command = connection.Container?.CommandLine();
            return await SshTerminalSession.StartAsync(client, "xterm-256color", 80, 24, CancellationToken.None, ownsClient: true, command, shellIntegration: connection.ShellIntegration != false).ConfigureAwait(true);
        }
        catch
        {
            await client.DisposeAsync().ConfigureAwait(true);
            throw;
        }
    }

    // Through via when given, a jump host this takes over: closing the new connection closes it too, and so does a failure here.
    private async Task<SshClient> LogInClientAsync(string name, SshHostConfig host, string address, int port, string user, SshClient? via)
    {
        var prompts = new SshLoginPrompts(this, name, _redactor, _chat.AddNotice);
        var knownHosts = new KnownHostsFile([.. host.UserKnownHostsFiles, .. host.GlobalKnownHostsFiles]);
        var verifier = new KnownHostsVerifier(knownHosts, prompts, name, host.Get("HostKeyAlias"), isStrict: host.IsYes("StrictHostKeyChecking"));
        var options = new SshConnectionOptions { Host = address, Port = port, UserName = user, HostKeyVerifier = verifier };
        var client = await ConnectAsync(options, via).ConfigureAwait(true);
        var (keys, notes) = await SshLoginPlan.FindKeysAsync(host, ProjectFolders.Home, CancellationToken.None).ConfigureAwait(true);
        try
        {
            foreach (var note in notes)
            {
                _chat.AddNotice(Strings.Format(Strings.NoticeLoginNote, name, note));
            }

            await client.AuthenticateAsync(keys, prompts, CancellationToken.None).ConfigureAwait(true);
            return client;
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

    private static async Task<SshClient> ConnectAsync(SshConnectionOptions options, SshClient? via)
    {
        if (via is null)
        {
            return await SshClient.ConnectAsync(options, CancellationToken.None).ConfigureAwait(true);
        }

        SshChannelStream stream;
        try
        {
            stream = new SshChannelStream(await via.OpenDirectTcpIpAsync(options.Host, options.Port, CancellationToken.None).ConfigureAwait(true), via);
        }
        catch
        {
            await via.DisposeAsync().ConfigureAwait(true);
            throw;
        }

        return await SshClient.ConnectAsync(options, stream, CancellationToken.None).ConfigureAwait(true);
    }

    // The jump hosts to go through, checked as the target is; null, after a notice, when the connection can't be made.
    private IReadOnlyList<SshJump>? JumpsFor(SshConfig config, SshHostConfig host)
    {
        var jumps = SshJump.Parse(host.Get("ProxyJump"), out var problem);
        var unsupported = problem
            ?? SshLoginPlan.Unsupported(host)
            ?? jumps!.Select(jump => config.Resolve(jump.Host)).Select(hop => NestedJump(hop) ?? SshLoginPlan.Unsupported(hop)).FirstOrDefault(text => text is not null);
        if (unsupported is null)
        {
            return jumps;
        }

        _chat.AddNotice(unsupported);
        return null;
    }

    private static string? NestedJump(SshHostConfig hop) =>
        hop.Get("ProxyJump") is { } nested && !string.Equals(nested, "none", StringComparison.OrdinalIgnoreCase)
            ? $"ProxyJump in your SSH configuration for the jump host {hop.Alias} isn't supported yet; list every hop in the target's ProxyJump instead."
            : null;

    // Production connections ask before every command unless the project says otherwise (product decision 72).
    private ApprovalMode DefaultModeFor(ProjectConnection connection) =>
        connection.ApprovalMode
        ?? (connection.IsProduction ? ApprovalMode.Ask : _settings.Current.DefaultApprovalMode);
}
