using Avalonia.Controls;
using Datapoint.Terminal.Agent.Projects;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Sessions.Ssh.Config;

namespace Datapoint.Terminal.Shell;

public sealed partial class MainWindow
{
    private void OnNewTabClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => _ = ShowNewTabMenuAsync();

    private async Task ShowNewTabMenuAsync()
    {
        await ReviewConnectionsAsync(reload: true).ConfigureAwait(true);
        var menu = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedRight };
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
        menu.Items.Add(SshHostsMenu(Project.Settings.Connections));
        menu.ShowAt(NewTabButton);
    }

    private MenuItem SshHostsMenu(IReadOnlyList<ProjectConnection> connections)
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
        if (!ProjectConnection.IsPlainLabel(alias))
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
        var connection = new ProjectConnection { Name = alias, SshHost = alias };
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

    private void AddConnection(ProjectConnection connection)
    {
        if (Project.IsReadOnly)
        {
            _chat.AddNotice(Strings.Format(Strings.NoticeProjectReadOnlyNotAdded, connection.Name));
            return;
        }

        try
        {
            Project.SaveSettings(Project.Settings with { Connections = [.. Project.Settings.Connections, connection] }, _redactor);
            Confirm([.. Connections, connection]);
            _chat.AddNotice(Strings.Format(Strings.NoticeHostAdded, connection.Name));
        }
        catch (Exception exception) when (exception is ProjectException or IOException or UnauthorizedAccessException)
        {
            _chat.AddNotice(exception.Message);
        }
    }
}
