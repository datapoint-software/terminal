using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Datapoint.Terminal.Agent.Projects;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Settings;

namespace Datapoint.Terminal.Shell;

public sealed class ProjectWindows
{
    private const int RecentLimit = 10;

    private readonly IClassicDesktopStyleApplicationLifetime _desktop;
    private readonly List<MainWindow> _windows = [];
    private SettingsWindow? _settingsWindow;
    private bool _isQuitting;

    public ProjectWindows(IClassicDesktopStyleApplicationLifetime desktop, SettingsViewModel settings)
    {
        ArgumentNullException.ThrowIfNull(desktop);
        ArgumentNullException.ThrowIfNull(settings);
        _desktop = desktop;
        Settings = settings;
        desktop.ShutdownRequested += (_, _) => _isQuitting = true;
    }

    public SettingsViewModel Settings { get; }

    public IReadOnlyList<string> Recent => Settings.RecentProjects;

    public void OpenAtStartup()
    {
        var problems = new List<string>();
        foreach (var folder in Settings.OpenProjects)
        {
            Open(folder, isNew: false, problems);
        }

        if (_windows.Count > 0)
        {
            return;
        }

        try
        {
            Show(Project.OpenDefault(), problems);
        }
        catch (ProjectException exception)
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
            Title = isNew ? Strings.PickerNewProject : Strings.PickerOpenProject,
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

    public void ClearRecent() => Settings.SetProjects([], OpenFolders());

    public void OpenSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow { DataContext = Settings };
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

    private MainWindow? Active => _windows.FirstOrDefault(window => window.IsActive) ?? _windows.LastOrDefault();

    private void Open(string folder, bool isNew, List<string> problems)
    {
        if (_windows.FirstOrDefault(window => ProjectFolders.AreSame(window.Project.Folder, folder)) is { } existing)
        {
            existing.Activate();
            return;
        }

        try
        {
            var project = Project.Open(folder);
            if (isNew && !project.IsNew)
            {
                problems.Add(Strings.Format(Strings.NoticeProjectIsNotNew, project.Name));
            }

            Show(project, []);
        }
        catch (ProjectException exception)
        {
            problems.Add(Strings.Format(Strings.NoticeProjectFailed, exception.Message));
        }
    }

    private void Show(Project project, IReadOnlyList<string> notices)
    {
        var window = new MainWindow(project, this);
        foreach (var notice in notices)
        {
            window.AddNotice(notice);
        }

        _windows.Add(window);
        _desktop.MainWindow ??= window;
        window.Closed += (_, _) => OnClosed(window);
        window.Show();
        Remember(project.Folder);
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

        Settings.SetProjects(Recent, OpenFolders());
    }

    private void Remember(string folder)
    {
        var recent = Recent.Where(item => !ProjectFolders.AreSame(item, folder)).Prepend(folder).Take(RecentLimit).ToList();
        Settings.SetProjects(recent, OpenFolders());
    }

    private List<string> OpenFolders() => [.. _windows.Select(window => window.Project.Folder)];
}
