using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Datapoint.Terminal.Agent.Workspaces;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Settings;

namespace Datapoint.Terminal.Shell;

public sealed class WorkspaceWindows
{
    private const int RecentLimit = 10;

    private readonly IClassicDesktopStyleApplicationLifetime _desktop;
    private readonly List<MainWindow> _windows = [];
    private SettingsWindow? _settingsWindow;
    private bool _isQuitting;

    public WorkspaceWindows(IClassicDesktopStyleApplicationLifetime desktop, SettingsViewModel settings)
    {
        ArgumentNullException.ThrowIfNull(desktop);
        ArgumentNullException.ThrowIfNull(settings);
        _desktop = desktop;
        Settings = settings;
        desktop.ShutdownRequested += OnShutdownRequested;
    }

    public SettingsViewModel Settings { get; }

    public InstructionSnapshots InstructionSnapshots { get; } =
        new(Path.Combine(Path.GetDirectoryName(SettingsStore.DefaultPath)!, "instructions-read.json"));

    public IReadOnlyList<string> Recent => Settings.RecentWorkspaces;

    public void OpenAtStartup()
    {
        var problems = new List<string>();
        foreach (var folder in Settings.OpenWorkspaces)
        {
            Open(folder, isNew: false, problems);
        }

        if (_windows.Count > 0)
        {
            return;
        }

        try
        {
            Show(Workspace.OpenDefault(), problems);
        }
        catch (WorkspaceException exception)
        {
            problems.Add(exception.Message);
            throw new InvalidOperationException(string.Join(Environment.NewLine, problems), exception);
        }
    }

    public void Open(string folder) => Open(folder, isNew: false, []);

    public async Task PickAsync(Window owner, bool isNew)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = isNew ? Strings.PickerNewWorkspace : Strings.PickerOpenWorkspace,
            AllowMultiple = false,
        }).ConfigureAwait(true);
        if (folders.Count == 0 || folders[0].TryGetLocalPath() is not { } path)
        {
            return;
        }

        var problems = new List<string>();
        Open(path, isNew, problems);
        foreach (var problem in problems)
        {
            (Active ?? owner as MainWindow)?.AddNotice(problem);
        }
    }

    public void ClearRecent() => Settings.SetWorkspaces([], OpenFolders());

    public void OpenSettings() => OpenSettings(page: null);

    public void OpenSettings(int? page)
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.History = Active;
            _settingsWindow.Memory = Active?.MemoryPlaces;
            if (page is { } shown)
            {
                _settingsWindow.ShowPage(shown);
            }

            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow { DataContext = Settings, History = Active, Memory = Active?.MemoryPlaces };
        if (page is { } first)
        {
            _settingsWindow.ShowPage(first);
        }

        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        if (Active is { } owner)
        {
            _settingsWindow.Show(owner);
        }
        else
        {
            _settingsWindow.Show();
        }
    }

    // The agent changed memory: an open Memory page shows it.
    public void ReloadMemory() => _settingsWindow?.ReloadMemory();

    private MainWindow? Active => _windows.FirstOrDefault(window => window.IsActive) ?? _windows.LastOrDefault();

    private void Open(string folder, bool isNew, List<string> problems)
    {
        if (_windows.FirstOrDefault(window => WorkspaceFolders.AreSame(window.Workspace.Folder, folder)) is { } existing)
        {
            existing.Activate();
            return;
        }

        try
        {
            var workspace = Workspace.Open(folder);
            if (isNew && !workspace.IsNew)
            {
                problems.Add(Strings.Format(Strings.NoticeWorkspaceIsNotNew, workspace.Name));
            }

            Show(workspace, []);
        }
        catch (WorkspaceException exception)
        {
            problems.Add(Strings.Format(Strings.NoticeWorkspaceFailed, exception.Message));
        }
    }

    private void Show(Workspace workspace, IReadOnlyList<string> notices)
    {
        var window = new MainWindow(workspace, this);
        foreach (var notice in notices)
        {
            window.AddNotice(notice);
        }

        _windows.Add(window);
        _desktop.MainWindow ??= window;
        window.Closed += (_, _) => OnClosed(window);
        window.Show();
        Remember(workspace.Folder);
    }

    private void OnClosed(MainWindow window)
    {
        _windows.Remove(window);
        if (ReferenceEquals(_desktop.MainWindow, window))
        {
            _desktop.MainWindow = _windows.FirstOrDefault();
        }

        if (_windows.Count == 0)
        {
            _settingsWindow?.Close();
        }

        if (_isQuitting || _windows.Count == 0)
        {
            return;
        }

        Settings.SetWorkspaces(Recent, OpenFolders());
    }

    // Quitting asks once for every window's busy tabs; only a confirmed quit counts as quitting.
    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        var busy = _windows.SelectMany(window => window.BusyTabs(withWorkspace: _windows.Count > 1)).ToList();
        if (_isQuitting || busy.Count == 0)
        {
            Quit();
            return;
        }

        e.Cancel = true;
        _ = ConfirmQuitAsync(string.Join(Environment.NewLine, busy));
    }

    private async Task ConfirmQuitAsync(string details)
    {
        if (Active is not { } owner)
        {
            return;
        }

        var choice = await ChoiceWindow.AskAsync(owner, Strings.QuitTitle, Strings.QuitMessage, details, hasDefault: true, Strings.Cancel, Strings.QuitConfirm).ConfigureAwait(true);
        if (choice != 1)
        {
            return;
        }

        Quit();
        _desktop.TryShutdown();
    }

    private void Quit()
    {
        _isQuitting = true;
        foreach (var window in _windows)
        {
            window.AllowClose();
        }
    }

    private void Remember(string folder)
    {
        var recent = Recent.Where(item => !WorkspaceFolders.AreSame(item, folder)).Prepend(folder).Take(RecentLimit).ToList();
        Settings.SetWorkspaces(recent, OpenFolders());
    }

    private List<string> OpenFolders() => [.. _windows.Select(window => window.Workspace.Folder)];
}
