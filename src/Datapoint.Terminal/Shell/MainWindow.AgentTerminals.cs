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
        if (string.Equals(name, Strings.TabLocal, StringComparison.OrdinalIgnoreCase))
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

    private void ConnectNotice(string text)
    {
        if (_isReconnecting)
        {
            return;
        }

        _chat.AddNotice(text);
    }
}
