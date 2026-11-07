using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Datapoint.Terminal.Agent.Projects;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Sessions.Ssh.Config;
using Datapoint.Terminal.Sidebar;

namespace Datapoint.Terminal.Shell;

// Product decision 98: the project's hosts and files beside the terminals.
public sealed partial class MainWindow
{
    private const double DefaultSidebarWidth = 240;

    private SidebarViewModel _sidebar = null!;
    private FileSystemWatcher? _projectWatcher;
    private readonly DispatcherTimer _filesTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private bool _isSidebarHidden;
    private double _sidebarWidth = DefaultSidebarWidth;
    private NativeMenuItem? _toggleSidebarItem;

    private string HostsFolder => Path.Combine(Project.Folder, "hosts");

    private void SetUpSidebar()
    {
        _sidebar = new SidebarViewModel(Project.Folder, HostsFolder)
        {
            HostChosen = OpenHostDocument,
            TerminalRequested = connection => _ = OpenConnectionAsync(connection),
            HostAdded = AddSshHost,
            FileChosen = node => OpenProjectFile(node.Path),
            NewFileRequested = () => _ = NewFileAsync(),
        };
        Sidebar.DataContext = _sidebar;
        _sidebar.RefreshFiles();
        RefreshSidebarHosts();
        _tabs.CollectionChanged += (_, _) => RefreshOpenTerminals();
        Tabs.SelectionChanged += (_, _) => _sidebar.ShowHost(SelectedTab?.ConnectionName);
        _filesTimer.Tick += (_, _) =>
        {
            _filesTimer.Stop();
            _sidebar.RefreshFiles();
        };
        WatchProject();
        SetUpDocuments();
        Closed += (_, _) =>
        {
            _filesTimer.Stop();
            _projectWatcher?.Dispose();
        };
    }

    // Changes to the project's files show without the user asking; the personal folder and Git's own change all the time and
    // aren't shown, so they're not watched for.
    private void WatchProject()
    {
        try
        {
            _projectWatcher = new FileSystemWatcher(Project.Folder)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
            };
            FileSystemEventHandler changed = (_, e) => OnProjectChanged(e.FullPath);
            _projectWatcher.Created += changed;
            _projectWatcher.Deleted += changed;
            _projectWatcher.Renamed += (_, e) => OnProjectChanged(e.FullPath);
            _projectWatcher.EnableRaisingEvents = true;
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or PlatformNotSupportedException or UnauthorizedAccessException)
        {
            // Without a watcher, the tree still refreshes when the window comes back to the front.
            _projectWatcher?.Dispose();
            _projectWatcher = null;
        }

        Activated += (_, _) => _sidebar.RefreshFiles();
    }

    private void OnProjectChanged(string path)
    {
        if (ProjectFolders.RelativeInside(Project.Folder, path) is not { } relative
            || relative.StartsWith(ProjectFolders.PersonalFolder + "/", StringComparison.Ordinal)
            || string.Equals(relative, ProjectFolders.PersonalFolder, StringComparison.Ordinal)
            || relative.Split('/').Contains(".git", StringComparer.Ordinal))
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
        var connections = Connections.Count > 0 ? Connections : Project.Settings.Connections;
        _sidebar.SetHosts(connections, OtherSshHosts(Project.Settings.Connections));
        RefreshOpenTerminals();
    }

    // Hosts in the user's SSH configuration the project doesn't list, with names plain enough to add.
    private static IReadOnlyList<string> OtherSshHosts(IReadOnlyList<ProjectConnection> connections)
    {
        var listed = connections.SelectMany(connection => new[] { connection.SshHost, connection.Name }).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        return [.. SshConfig.Load().HostAliases().Where(alias => !listed.Contains(alias) && ProjectConnection.IsPlainLabel(alias))];
    }

    private void RefreshOpenTerminals() => _sidebar.SetOpenTerminals(_tabs
        .Where(tab => tab.IsRemote || !string.Equals(tab.ConnectionName, Strings.TabLocal, StringComparison.Ordinal))
        .GroupBy(tab => tab.ConnectionName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase));

    private void AddSshHost(string alias)
    {
        if (!ProjectConnection.IsPlainLabel(alias))
        {
            return;
        }

        AddConnection(new ProjectConnection { Name = alias, SshHost = alias });
        RefreshSidebarHosts();
    }

    private void OpenHostDocument(ProjectConnection connection)
    {
        var path = CreateHostDocs().FileFor(connection.Name);
        if (!File.Exists(path) && Project.IsReadOnly)
        {
            _chat.AddNotice(Strings.Format(Strings.SidebarNoDocumentation, connection.Name));
            return;
        }

        OpenProjectFile(path);
    }

    // A new file goes in the folder selected in the tree, or at the top of the project.
    private async Task NewFileAsync()
    {
        if (Project.IsReadOnly)
        {
            _chat.AddNotice(Strings.SidebarReadOnly);
            return;
        }

        var folder = _sidebar.SelectedFile is { } node ? (node.IsFolder ? node.Path : Path.GetDirectoryName(node.Path)!) : Project.Folder;
        var answers = await PromptWindow.AskAsync(this, Strings.SidebarNewFileTitle, Strings.Format(Strings.SidebarNewFileMessage, ProjectFolders.RelativeInside(Project.Folder, folder) is { Length: > 0 } relative ? relative : Project.Name), serverText: null, [(Strings.SidebarNewFileName, false)], ["notes.md"]).ConfigureAwait(true);
        if (answers is not [var name] || NewFileProblem(name) is { } problem)
        {
            if (answers is [var given] && NewFileProblem(given) is { } reason)
            {
                _chat.AddNotice(reason);
            }

            return;
        }

        var path = Path.Combine(folder, name.Trim());
        try
        {
            using (new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            {
            }

            _sidebar.RefreshFiles();
            OpenProjectFile(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _chat.AddNotice(Strings.Format(Strings.SidebarNewFileFailed, name.Trim(), exception.Message));
        }
    }

    // One plain name, so a new file can't land outside the folder or in the personal one.
    private static string? NewFileProblem(string name)
    {
        var trimmed = name.Trim();
        return trimmed.Length == 0 || trimmed is "." or ".." || trimmed.IndexOfAny([.. Path.GetInvalidFileNameChars(), '/', '\\']) >= 0
            ? Strings.Format(Strings.SidebarNewFileInvalid, trimmed)
            : null;
    }

    private void ToggleSidebar()
    {
        var column = Workspace.ColumnDefinitions[0];
        if (!_isSidebarHidden && column.ActualWidth > 0)
        {
            _sidebarWidth = column.ActualWidth;
        }

        _isSidebarHidden = !_isSidebarHidden;
        column.Width = new GridLength(_isSidebarHidden ? 0 : _sidebarWidth);
        column.MinWidth = _isSidebarHidden ? 0 : 160;
        Workspace.ColumnDefinitions[1].Width = new GridLength(_isSidebarHidden ? 0 : 1);
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
