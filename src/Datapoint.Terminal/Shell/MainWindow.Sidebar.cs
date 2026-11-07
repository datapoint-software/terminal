using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Datapoint.Terminal.Agent.Workspaces;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Sessions.Ssh.Config;
using Datapoint.Terminal.Sidebar;

namespace Datapoint.Terminal.Shell;

// Product decisions 98 and 100: the workspace's hosts, skills and instructions beside the terminals.
public sealed partial class MainWindow
{
    private const double DefaultSidebarWidth = 240;

    private SidebarViewModel _sidebar = null!;
    private FileSystemWatcher? _workspaceWatcher;
    private readonly DispatcherTimer _filesTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private bool _isSidebarHidden;
    private double _sidebarWidth = DefaultSidebarWidth;
    private NativeMenuItem? _toggleSidebarItem;

    private string HostsFolder => Path.Combine(Workspace.Folder, "hosts");

    private string InstructionsPath => Path.Combine(Workspace.Folder, InstructionFiles.FileName);

    private void SetUpSidebar()
    {
        _sidebar = new SidebarViewModel(Workspace.Folder)
        {
            HostChosen = OpenHostDocument,
            TerminalRequested = connection => _ = OpenConnectionAsync(connection),
            HostRemoved = connection => _ = RemoveHostAsync(connection),
            HostAdded = AddSshHost,
            SkillChosen = skill => OpenWorkspaceFile(skill.Path),
            InstructionsRequested = () => OpenWorkspaceFile(InstructionsPath),
            NewHostRequested = () => _ = NewHostAsync(),
            SettingsRequested = _windows.OpenSettings,
            ListWorkspaces = ListWorkspaces,
            WorkspaceChosen = workspace => _windows.Open(workspace.Folder),
            NewWorkspaceRequested = () => _ = _windows.PickAsync(this, isNew: true),
            OpenWorkspaceRequested = () => _ = _windows.PickAsync(this, isNew: false),
            RevealRequested = () => _ = Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(Workspace.Folder)),
        };
        Sidebar.DataContext = _sidebar;
        _sidebar.RefreshSkills();
        RefreshSidebarHosts();
        _tabs.CollectionChanged += (_, _) => RefreshOpenTerminals();
        Tabs.SelectionChanged += (_, _) => _sidebar.ShowHost(SelectedTab?.ConnectionName);
        _filesTimer.Tick += (_, _) =>
        {
            _filesTimer.Stop();
            _sidebar.RefreshSkills();
        };
        WatchWorkspace();
        SetUpDocuments();
        Closed += (_, _) =>
        {
            _filesTimer.Stop();
            _workspaceWatcher?.Dispose();
        };
    }

    private static string LocationOf(string folder) =>
        WorkspaceFolders.AreSame(folder, WorkspaceFolders.DefaultWorkspace) ? Strings.SidebarDefaultWorkspace : WorkspaceLocation.Describe(folder, WorkspaceFolders.Home);

    // This workspace, then the recent ones, then the default one, which is always there to go back to.
    private List<WorkspaceSwitchItem> ListWorkspaces()
    {
        var folders = new List<string> { Workspace.Folder };
        foreach (var folder in _windows.Recent.Append(WorkspaceFolders.DefaultWorkspace))
        {
            if (!folders.Any(known => WorkspaceFolders.AreSame(known, folder)) && (Directory.Exists(folder) || WorkspaceFolders.AreSame(folder, WorkspaceFolders.DefaultWorkspace)))
            {
                folders.Add(folder);
            }
        }

        return [.. folders.Select(folder => new WorkspaceSwitchItem(
            WorkspaceFolders.AreSame(folder, WorkspaceFolders.DefaultWorkspace) ? Strings.SidebarDefaultWorkspace : Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar)),
            WorkspaceFolders.AreSame(folder, Workspace.Folder) ? _sidebar.WorkspaceDetail : LocationOf(folder),
            folder,
            WorkspaceFolders.AreSame(folder, Workspace.Folder)))];
    }

    // Changes to the workspace's skills show without the user asking; the folder is watched as a whole because a skill is
    // a folder that may arrive before its SKILL.md does.
    private void WatchWorkspace()
    {
        try
        {
            _workspaceWatcher = new FileSystemWatcher(Workspace.Folder)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
            };
            FileSystemEventHandler changed = (_, e) => OnWorkspaceChanged(e.FullPath);
            _workspaceWatcher.Created += changed;
            _workspaceWatcher.Deleted += changed;
            _workspaceWatcher.Changed += changed;
            _workspaceWatcher.Renamed += (_, e) => OnWorkspaceChanged(e.FullPath);
            _workspaceWatcher.EnableRaisingEvents = true;
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or PlatformNotSupportedException or UnauthorizedAccessException)
        {
            // Without a watcher, the list still refreshes when the window comes back to the front.
            _workspaceWatcher?.Dispose();
            _workspaceWatcher = null;
        }

        Activated += (_, _) =>
        {
            _sidebar.RefreshSkills();
            RefreshHostLogs();
        };
    }

    private void OnWorkspaceChanged(string path)
    {
        if (WorkspaceFolders.RelativeInside(Workspace.Folder, path) is not { } relative
            || !relative.StartsWith("skills/", StringComparison.Ordinal))
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            _filesTimer.Stop();
            _filesTimer.Start();
        });
    }

    private void RefreshSidebarHosts()
    {
        var connections = Connections.Count > 0 ? Connections : Workspace.Settings.Connections;
        _sidebar.SetHosts(connections, OtherSshHosts(Workspace.Settings.Connections));
        var count = connections.Count == 1 ? Strings.SidebarHostCountOne : Strings.Format(Strings.SidebarHostCount, connections.Count);
        _sidebar.SetWorkspace(Workspace.Name, $"{LocationOf(Workspace.Folder)} · {count}");
        RefreshOpenTerminals();
    }

    // Hosts in the user's SSH configuration the workspace doesn't list, with names plain enough to add.
    private static IReadOnlyList<string> OtherSshHosts(IReadOnlyList<WorkspaceConnection> connections)
    {
        var listed = connections.SelectMany(connection => new[] { connection.SshHost, connection.Name }).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        return [.. SshConfig.Load().HostAliases().Where(alias => !listed.Contains(alias) && WorkspaceConnection.IsPlainLabel(alias))];
    }

    private void RefreshOpenTerminals() => _sidebar.SetOpenTerminals(_tabs
        .Where(tab => tab.IsRemote || !string.Equals(tab.ConnectionName, Strings.TabLocal, StringComparison.Ordinal))
        .GroupBy(tab => tab.ConnectionName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase));

    private void AddSshHost(string alias)
    {
        if (!WorkspaceConnection.IsPlainLabel(alias))
        {
            return;
        }

        AddConnection(new WorkspaceConnection { Name = alias, SshHost = alias });
        RefreshSidebarHosts();
    }

    private void OpenHostDocument(WorkspaceConnection connection)
    {
        var path = CreateHostDocs().FileFor(connection.Name);
        if (!File.Exists(path) && Workspace.IsReadOnly)
        {
            _chat.AddNotice(Strings.Format(Strings.SidebarNoDocumentation, connection.Name));
            return;
        }

        OpenWorkspaceFile(path);
    }

    private void ToggleSidebar()
    {
        var column = WorkArea.ColumnDefinitions[0];
        if (!_isSidebarHidden && column.ActualWidth > 0)
        {
            _sidebarWidth = column.ActualWidth;
        }

        _isSidebarHidden = !_isSidebarHidden;
        column.Width = new GridLength(_isSidebarHidden ? 0 : _sidebarWidth);
        column.MinWidth = _isSidebarHidden ? 0 : 160;
        WorkArea.ColumnDefinitions[1].Width = new GridLength(_isSidebarHidden ? 0 : 1);
        Sidebar.IsVisible = SidebarSeparator.IsVisible = SidebarSplitter.IsVisible = !_isSidebarHidden;
        _toggleSidebarItem?.Header = _isSidebarHidden ? Strings.MenuShowSidebar : Strings.MenuHideSidebar;
        if (!_isSidebarHidden)
        {
            return;
        }

        SelectedTab?.View.Focus();
    }

    private NativeMenuItem SidebarMenuItem()
    {
        _toggleSidebarItem = Item(Strings.MenuHideSidebar, Shortcut(Key.B, shift: true), ToggleSidebar);
        return _toggleSidebarItem;
    }
}
