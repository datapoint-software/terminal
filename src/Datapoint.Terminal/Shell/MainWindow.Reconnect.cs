using System.Net.Sockets;
using Datapoint.Terminal.Agent.Files;
using Datapoint.Terminal.Agent.Workspaces;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Sessions.Ssh;
using Datapoint.Terminal.Sessions.Ssh.Authentication;
using Datapoint.Terminal.Sessions.Ssh.Channels;
using Datapoint.Terminal.Sessions.Ssh.Config;
using Datapoint.Terminal.Sessions.Ssh.Protocol;

namespace Datapoint.Terminal.Shell;

// Product decision 124: when the connection to a host drops, e.g. because it restarted, the tab stays and logs in again in
// the same terminal, so what it showed stays above. A shell that exited, because the user or a program ended it, still
// closes its tab, as a local one does.
public sealed partial class MainWindow
{
    private static readonly TimeSpan s_firstRetry = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan s_longestRetry = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan s_retryFor = TimeSpan.FromMinutes(10);

    // One tab logs in at a time, so the splits of a dropped connection share the first one's new connection rather than
    // each logging in, and asking for a password, again.
    private bool _isLoggingInAgain;

    private enum Reconnection
    {
        Done,
        Failed,
        Refused,
        Closed,
    }

    private async Task OnConnectionEndedAsync(TerminalTab tab)
    {
        if (!_tabs.Contains(tab) || tab.IsDisconnected)
        {
            return;
        }

        if (tab.SharedClient is null || await ExitCodeAsync(tab).ConfigureAwait(true) is not null)
        {
            CloseTab(tab);
            return;
        }

        tab.IsDisconnected = true;
        tab.LostClient = tab.SharedClient;
        if (tab.AgentTerminal is { } gone)
        {
            _agent?.RemoveTerminal(gone);
        }

        foreach (var forward in tab.Forwards)
        {
            await forward.DisposeAsync().ConfigureAwait(true);
        }

        tab.Forwards.Clear();
        Note(tab, Strings.Format(Strings.TerminalConnectionLost, tab.ConnectionName));
        await KeepReconnectingAsync(tab).ConfigureAwait(true);
    }

    // Null when the connection dropped: the shell never said how it ended.
    private static async Task<int?> ExitCodeAsync(TerminalTab tab)
    {
        if (tab.Connection is not { } connection)
        {
            return null;
        }

        var exited = connection.Exited;
        return await Task.WhenAny(exited, Task.Delay(TimeSpan.FromSeconds(2))).ConfigureAwait(true) == exited ? await exited.ConfigureAwait(true) : null;
    }

    private async Task KeepReconnectingAsync(TerminalTab tab)
    {
        if (tab.IsRetrying)
        {
            return;
        }

        tab.IsRetrying = true;
        var started = DateTimeOffset.UtcNow;
        var wait = s_firstRetry;
        try
        {
            while (true)
            {
                var outcome = await ReconnectOnceAsync(tab).ConfigureAwait(true);
                if (outcome is Reconnection.Done or Reconnection.Closed)
                {
                    return;
                }

                if (outcome == Reconnection.Refused || DateTimeOffset.UtcNow - started >= s_retryFor)
                {
                    Note(tab, Strings.Format(Strings.TerminalReconnectStopped, tab.ConnectionName));
                    return;
                }

                await Task.Delay(wait).ConfigureAwait(true);
                wait = TimeSpan.FromTicks(Math.Min(wait.Ticks * 2, s_longestRetry.Ticks));
            }
        }
        finally
        {
            tab.IsRetrying = false;
        }
    }

    private void RetryOnEnter(TerminalTab tab, byte[] data)
    {
        if (tab.IsRetrying || !data.Contains((byte)'\r'))
        {
            return;
        }

        Note(tab, Strings.Format(Strings.TerminalReconnecting, tab.ConnectionName));
        _ = KeepReconnectingAsync(tab);
    }

    private async Task<Reconnection> ReconnectOnceAsync(TerminalTab tab)
    {
        while (_isLoggingInAgain)
        {
            await Task.Delay(200).ConfigureAwait(true);
        }

        var connection = Connections.FirstOrDefault(item => string.Equals(item.Name, tab.ConnectionName, StringComparison.OrdinalIgnoreCase));
        if (!_tabs.Contains(tab) || connection is null)
        {
            return _tabs.Contains(tab) ? Reconnection.Refused : Reconnection.Closed;
        }

        _isLoggingInAgain = true;
        _isReconnecting = true;
        try
        {
            return await LogInAgainAsync(tab, connection).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is SshAuthenticationException or OperationCanceledException)
        {
            return Reconnection.Refused;
        }
        catch (Exception exception) when (exception is SshDisconnectedException or SshProtocolException or SshChannelException
            or SocketException or IOException or TimeoutException)
        {
            return Reconnection.Failed;
        }
        finally
        {
            _isLoggingInAgain = false;
            _isReconnecting = false;
        }
    }

    private async Task<Reconnection> LogInAgainAsync(TerminalTab tab, WorkspaceConnection connection)
    {
        SshHostConfig? host = null;
        var (session, shared) = await SessionOnSharedAsync(tab, connection).ConfigureAwait(true);
        if (session is null)
        {
            var config = SshConfig.Load();
            host = config.Resolve(connection.SshHost ?? connection.Host!);
            if (JumpsFor(config, host) is not { } jumps)
            {
                return Reconnection.Refused;
            }

            (session, shared) = await LogInAsync(connection, config, host, jumps).ConfigureAwait(true);
        }

        return await ResumeAsync(tab, connection, session, shared!, host).ConfigureAwait(true);
    }

    private async Task<Reconnection> ResumeAsync(TerminalTab tab, WorkspaceConnection connection, SshTerminalSession session, SharedSshClient shared, SshHostConfig? host)
    {
        if (!_tabs.Contains(tab) || tab.Connection is not { } terminal)
        {
            await session.DisposeAsync().ConfigureAwait(true);
            return Reconnection.Closed;
        }

        Note(tab, Strings.Format(Strings.TerminalReconnected, connection.Name));
        await terminal.ResumeAsync(session).ConfigureAwait(true);
        await terminal.ResizeAsync(terminal.Terminal.Columns, terminal.Terminal.Rows, CancellationToken.None).ConfigureAwait(true);
        var onHost = connection.Container is null;
        tab.Client = onHost ? session.Client : null;
        tab.SharedClient = shared;
        tab.IsDisconnected = false;
        tab.AgentTerminal = NewAgentTerminal(tab, connection.Environment, onHost ? new SshHostFiles(session.Client) : null, connection.Container?.Describe());
        _agent?.AddTerminal(tab.AgentTerminal);
        if (host is not null)
        {
            StartForwards(tab, session.Client, [.. host.GetAll("LocalForward"), .. connection.Forwards ?? []]);
        }

        return Reconnection.Done;
    }

    // A split that shared the dropped connection takes the one the first of them logged in with.
    private async Task<(SshTerminalSession? Session, SharedSshClient? Shared)> SessionOnSharedAsync(TerminalTab tab, WorkspaceConnection connection)
    {
        var sibling = _tabs.FirstOrDefault(other => other != tab && !other.IsDisconnected && other.LostClient is not null
            && ReferenceEquals(other.LostClient, tab.LostClient) && other.SharedClient is { IsClosed: false });
        if (sibling?.SharedClient is not { } shared)
        {
            return (null, null);
        }

        var session = await SshTerminalSession.StartAsync(shared, "xterm-256color", 80, 24, connection.Container?.CommandLine(), connection.ShellIntegration != false, CancellationToken.None).ConfigureAwait(true);
        return (session, session is null ? null : shared);
    }

    // A faint line in the terminal itself, where the user is looking, kept apart from what the host prints. It goes on the
    // main screen, which a full-screen program the connection took with it can no longer give back.
    private static void Note(TerminalTab tab, string text)
    {
        tab.Connection?.Terminal.Feed($"\e[?1049l\r\n\e[2m{text}\e[0m\r\n");
        tab.View.NotifyOutput();
    }
}
