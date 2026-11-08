using Avalonia.Threading;
using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Shell;

// Terminals the agent opens: a local shell, or one of the workspace's connections as the user last confirmed it. A host from
// ~/.ssh/config that the workspace doesn't list isn't offered; the user opens and adds those.
public sealed partial class MainWindow
{
    IReadOnlyList<AgentConnection> ITerminalOpener.Connections =>
    [
        new AgentConnection(Strings.TabLocal, Environment: null) { IsLocalShell = true },
        .. Connections.Select(connection => new AgentConnection(connection.Name, connection.Environment, connection.Container?.Describe()) { Labels = connection.Labels ?? [] }),
    ];

    Task<string?> ITerminalOpener.OpenAsync(string connection, CancellationToken cancellationToken) =>
        Dispatcher.UIThread.InvokeAsync(() => OpenForAgentAsync(connection));

    // Connections are reviewed first, as from the New Tab menu, so a looser change from outside still waits for the user.
    private async Task<string?> OpenForAgentAsync(string name)
    {
        if (Strings.MatchesAny(nameof(Strings.TabLocal), name, StringComparison.OrdinalIgnoreCase))
        {
            return (await OpenLocalTabAsync(select: false).ConfigureAwait(true))?.Name;
        }

        await ReviewConnectionsAsync(reload: true).ConfigureAwait(true);
        var connection = Connections.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        if (connection is null)
        {
            _chat.AddNotice(Strings.Format(Strings.NoticeAgentConnectionGone, name));
            return null;
        }

        return (await OpenConnectionAsync(connection, select: false).ConfigureAwait(true))?.Name;
    }

    private bool _isReconnecting;

    Task<string?> ITerminalOpener.ReconnectAsync(string connection, CancellationToken cancellationToken) =>
        Dispatcher.UIThread.InvokeAsync(() => ReconnectForAgentAsync(connection));

    // A host coming back from a reboot during a rollout is tried every few seconds, so the attempts that fail stay out of the
    // chat; prompts, e.g. for a password, still show, since only the user can answer them.
    private async Task<string?> ReconnectForAgentAsync(string name)
    {
        // A tab whose connection dropped logs in again itself (product decision 124); the agent waits for it rather than
        // opening another.
        if (_tabs.FirstOrDefault(tab => tab.IsDisconnected && string.Equals(tab.ConnectionName, name, StringComparison.OrdinalIgnoreCase)) is { } dropped)
        {
            return await WaitForReconnectAsync(dropped).ConfigureAwait(true);
        }

        await ReviewConnectionsAsync(reload: true).ConfigureAwait(true);
        if (Connections.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)) is not { } connection)
        {
            return null;
        }

        _isReconnecting = true;
        try
        {
            return (await OpenConnectionAsync(connection, select: false).ConfigureAwait(true))?.Name;
        }
        finally
        {
            _isReconnecting = false;
        }
    }

    // Starts trying again if the tab had given up; null if it isn't back within a little while, and the agent asks again.
    private async Task<string?> WaitForReconnectAsync(TerminalTab tab)
    {
        _ = KeepReconnectingAsync(tab);
        var until = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(15);
        while (tab.IsDisconnected && tab.IsRetrying && _tabs.Contains(tab) && DateTimeOffset.UtcNow < until)
        {
            await Task.Delay(250).ConfigureAwait(true);
        }

        return tab.IsDisconnected || !_tabs.Contains(tab) ? null : tab.Name;
    }

    private void ConnectNotice(string text)
    {
        if (_isReconnecting)
        {
            return;
        }

        _chat.AddNotice(text);
    }
}
