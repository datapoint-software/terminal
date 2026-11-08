using Avalonia.Controls;
using Datapoint.Terminal.Agent.Workspaces;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Sessions.Ssh.Config;

namespace Datapoint.Terminal.Shell;

public sealed partial class MainWindow
{
    private void OnNewTabClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => _ = ShowNewTabMenuAsync(NewTabButton, PlacementMode.BottomEdgeAlignedRight);

    private void OnOpenTerminalClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => _ = ShowNewTabMenuAsync(OpenTerminalButton, PlacementMode.Bottom);

    private async Task ShowNewTabMenuAsync(Control anchor, PlacementMode placement)
    {
        await ReviewConnectionsAsync(reload: true).ConfigureAwait(true);
        var menu = new MenuFlyout { Placement = placement };
        menu.Items.Add(MenuItem(Strings.NewTabLocal, () => _ = OpenLocalTabAsync()));
        var connections = Connections;
        if (connections.Count > 0)
        {
            menu.Items.Add(new Separator());
            foreach (var connection in connections)
            {
                var label = connection.Environment is null ? connection.Name : $"{connection.Name} · {connection.Environment}";
                menu.Items.Add(MenuItem(label, () => _ = OpenConnectionAsync(connection)));
            }
        }

        menu.Items.Add(new Separator());
        menu.Items.Add(SshHostsMenu(Workspace.Settings.Connections));
        if (!Workspace.IsReadOnly)
        {
            menu.Items.Add(MenuItem(Strings.NewHostMenu, () => _ = NewHostAsync()));
        }

        menu.ShowAt(anchor);
    }

    private MenuItem SshHostsMenu(IReadOnlyList<WorkspaceConnection> connections)
    {
        var config = SshConfig.Load();
        var listed = connections.Select(connection => connection.SshHost).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var others = config.HostAliases().Where(alias => !listed.Contains(alias)).ToList();
        var menu = new MenuItem { Header = Strings.NewTabSshHosts };
        foreach (var alias in others)
        {
            menu.Items.Add(MenuItem(alias, () => _ = OfferToAddAsync(alias)));
        }

        if (others.Count == 0)
        {
            menu.Items.Add(new MenuItem { Header = Strings.NewTabNoSshHosts, IsEnabled = false });
        }

        return menu;
    }

    private static MenuItem MenuItem(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    private async Task OfferToAddAsync(string alias)
    {
        if (!WorkspaceConnection.IsPlainLabel(alias))
        {
            return;
        }

        var choice = await ChoiceWindow.AskAsync(
            this,
            Strings.Format(Strings.AddHostTitle, alias),
            Strings.Format(Strings.AddHostMessage, alias),
            Strings.Cancel,
            Strings.AddHostOpenOnce,
            Strings.AddHostAdd).ConfigureAwait(true);
        var connection = new WorkspaceConnection { Name = alias, SshHost = alias };
        if (choice == 2)
        {
            AddConnection(connection);
        }

        if (choice is not (1 or 2))
        {
            return;
        }

        await OpenConnectionAsync(connection).ConfigureAwait(true);
    }

    private void AddConnection(WorkspaceConnection connection)
    {
        if (Workspace.IsReadOnly)
        {
            _chat.AddNotice(Strings.Format(Strings.NoticeWorkspaceReadOnlyNotAdded, connection.Name));
            return;
        }

        try
        {
            Workspace.SaveSettings(Workspace.Settings with { Connections = [.. Workspace.Settings.Connections, connection] }, _redactor);
            Confirm([.. Connections, connection]);
            _chat.AddNotice(Strings.Format(Strings.NoticeHostAdded, connection.Name));
        }
        catch (Exception exception) when (exception is WorkspaceException or IOException or UnauthorizedAccessException)
        {
            _chat.AddNotice(exception.Message);
        }
    }

    // A host typed in by the user, which then opens straight away: adding one is nearly always to work on it.
    private async Task NewHostAsync()
    {
        if (Workspace.IsReadOnly)
        {
            _chat.AddNotice(Strings.NewHostReadOnly);
            return;
        }

        var aliases = OtherSshHosts(Workspace.Settings.Connections);
        var connection = await NewHostWindow.AskAsync(this, Workspace.Settings.Connections, aliases).ConfigureAwait(true);
        if (connection is null)
        {
            return;
        }

        AddConnection(connection);
        RefreshSidebarHosts();
        await OpenConnectionAsync(connection).ConfigureAwait(true);
    }

    // Only the workspace's list changes: the host's notes are worth keeping, and a terminal open on it is the user's to close.
    private async Task RemoveHostAsync(WorkspaceConnection connection)
    {
        if (Workspace.IsReadOnly)
        {
            _chat.AddNotice(Strings.Format(Strings.NoticeWorkspaceReadOnlyNotRemoved, connection.Name));
            return;
        }

        var choice = await ChoiceWindow.AskAsync(
            this,
            Strings.Format(Strings.RemoveHostTitle, connection.Name),
            Strings.RemoveHostMessage,
            details: null,
            hasDefault: false,
            Strings.Cancel,
            Strings.RemoveHostAction).ConfigureAwait(true);
        if (choice != 1)
        {
            return;
        }

        try
        {
            Workspace.SaveSettings(Workspace.Settings with { Connections = [.. Workspace.Settings.Connections.Where(item => !IsNamed(item, connection.Name))] }, _redactor);
            Confirm([.. Connections.Where(item => !IsNamed(item, connection.Name))]);
            _chat.AddNotice(Strings.Format(Strings.NoticeHostRemoved, connection.Name));
        }
        catch (Exception exception) when (exception is WorkspaceException or IOException or UnauthorizedAccessException)
        {
            _chat.AddNotice(exception.Message);
        }
    }

    private WorkspaceConnection? HostNamed(string name) => Connections.FirstOrDefault(connection => IsNamed(connection, name));

    // Everything but the name, which is also the folder of the host's documentation. Saving it is the user confirming it, so
    // a looser value isn't held for review the way a change from outside is (product decision 92).
    private async Task EditHostAsync(string name)
    {
        if (Workspace.IsReadOnly)
        {
            _chat.AddNotice(Strings.Format(Strings.NoticeWorkspaceReadOnlyNotEdited, name));
            return;
        }

        await ReviewConnectionsAsync(reload: true).ConfigureAwait(true);
        if (HostNamed(name) is not { } host)
        {
            return;
        }

        var all = SshConfig.Load().HostAliases().ToList();
        var edited = await NewHostWindow.EditAsync(this, host, Connections, OtherSshHosts(Connections), all).ConfigureAwait(true);
        if (edited is null || edited == host)
        {
            return;
        }

        try
        {
            var saved = Workspace.Settings.Connections;
            Workspace.SaveSettings(Workspace.Settings with { Connections = saved.Any(item => IsNamed(item, name)) ? [.. saved.Select(item => IsNamed(item, name) ? edited : item)] : [.. saved, edited] }, _redactor);
            Confirm([.. Connections.Select(item => IsNamed(item, name) ? edited : item)]);
        }
        catch (Exception exception) when (exception is WorkspaceException or IOException or UnauthorizedAccessException)
        {
            _chat.AddNotice(exception.Message);
            return;
        }

        ShowEditedHost(host, edited);
    }

    private void ShowEditedHost(WorkspaceConnection before, WorkspaceConnection after)
    {
        var open = _tabs.Where(tab => string.Equals(tab.ConnectionName, after.Name, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var tab in open)
        {
            tab.ChangeEnvironment(after.Environment);
        }

        foreach (var header in AllDocuments.OfType<Editor.DocumentViewModel>().Select(document => document.Host).OfType<Editor.HostHeaderViewModel>().Where(header => IsNamed(after, header.Name)))
        {
            header.Environment = after.Environment;
            header.Labels = after.Labels ?? [];
            header.Address = AddressOf(after);
        }

        var moved = !string.Equals(before.SshHost, after.SshHost, StringComparison.Ordinal)
            || !string.Equals(before.Host, after.Host, StringComparison.Ordinal)
            || !string.Equals(before.User, after.User, StringComparison.Ordinal)
            || before.Port != after.Port;
        _chat.AddNotice(Strings.Format(moved && open.Count > 0 ? Strings.NoticeHostEditedOpen : Strings.NoticeHostEdited, after.Name));
    }

    private static bool IsNamed(WorkspaceConnection connection, string name) => string.Equals(connection.Name, name, StringComparison.OrdinalIgnoreCase);
}
