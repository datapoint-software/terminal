using System.Text.Json;
using Datapoint.Terminal.Agent.Safety;

namespace Datapoint.Terminal.Agent.Workspaces;

public sealed class Workspace : IDisposable
{
    private const string LockFile = "lock";

    private readonly FileStream? _lock;

    private Workspace(string folder, string personal, WorkspaceSettings settings, FileStream? heldLock, bool isDefault, bool isNew)
    {
        Folder = folder;
        PersonalPath = personal;
        Settings = settings;
        _lock = heldLock;
        IsDefault = isDefault;
        IsNew = isNew;
    }

    public string Folder { get; }

    public string Name => IsDefault ? "Default workspace" : Path.GetFileName(Folder);

    public bool IsDefault { get; }

    public bool IsNew { get; }

    public bool IsReadOnly => _lock is null;

    public WorkspaceSettings Settings { get; private set; }

    public IReadOnlyList<string> Problems { get; private set; } = [];

    public string SettingsPath => Path.Combine(Folder, WorkspaceFolders.SettingsFile);

    public string PersonalPath { get; }

    public static Workspace OpenDefault() => Open(WorkspaceFolders.DefaultWorkspace, WorkspaceFolders.Home, create: true);

    public static Workspace Open(string folder) => Open(folder, WorkspaceFolders.Home, create: false);

    public static Workspace Open(string folder, string home, bool create, string? personalRoot = null)
    {
        var normalized = WorkspaceFolders.Normalize(folder);
        var isDefault = WorkspaceFolders.AreSame(normalized, WorkspaceFolders.DefaultWorkspace);
        if (create)
        {
            Directory.CreateDirectory(normalized);
        }

        if (WorkspaceFolders.WhyNotAWorkspace(normalized, home) is { } problem)
        {
            throw new WorkspaceException(problem);
        }

        var isNew = !WorkspaceFolders.IsWorkspace(normalized);
        var personal = WorkspaceFolders.PersonalData(personalRoot ?? WorkspaceFolders.PersonalRoot, normalized);
        try
        {
            CreatePrivateFolder(personal);
            var heldLock = TryLock(personal);
            if (heldLock is not null)
            {
                MoveLegacyLayout(normalized, personal);
            }

            var settingsPath = Path.Combine(normalized, WorkspaceFolders.SettingsFile);
            var (settings, problems) = ReadValidSettings(settingsPath);
            if (heldLock is not null && !File.Exists(settingsPath))
            {
                File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings, WorkspaceJsonContext.Default.WorkspaceSettings));
            }

            return new Workspace(normalized, personal, settings, heldLock, isDefault, isNew) { Problems = problems };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new WorkspaceException($"The workspace in {normalized} couldn't be opened: {exception.Message}", exception);
        }
    }

    // Reads workspace.json again, e.g. after a sync; returns its problems, and keeps the old settings if it can't be read.
    public IReadOnlyList<string> ReloadSettings()
    {
        try
        {
            var (settings, problems) = ReadValidSettings(SettingsPath);
            Settings = settings;
            return problems;
        }
        catch (Exception exception) when (exception is WorkspaceException or IOException or UnauthorizedAccessException)
        {
            return [exception.Message];
        }
    }

    public void SaveSettings(WorkspaceSettings settings, SecretRedactor detector)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(detector);
        if (IsReadOnly)
        {
            throw new WorkspaceException("This workspace is open in another copy of Datapoint Terminal, so it's read-only here.");
        }

        var json = JsonSerializer.Serialize(settings, WorkspaceJsonContext.Default.WorkspaceSettings);
        if (!string.Equals(detector.Redact(json), json, StringComparison.Ordinal))
        {
            throw new WorkspaceException($"{WorkspaceFolders.SettingsFile} is shared with the workspace and can't hold a secret, so it wasn't saved.");
        }

        var temporary = Path.Combine(Folder, $".terminal-{Guid.NewGuid():N}.tmp");
        File.WriteAllText(temporary, json);
        File.Move(temporary, SettingsPath, overwrite: true);
        Settings = settings;
    }

    public void Dispose() => _lock?.Dispose();

    // Before workspaces, a project kept its settings in .terminal.json and personal data in .terminal/ inside the folder. Both
    // move once: the settings to workspace.json, and the personal data next to the workspace's other data in the application's
    // own folder, so none of it stays where a sync service or a commit could take it.
    private static void MoveLegacyLayout(string folder, string personal)
    {
        var legacySettings = Path.Combine(folder, WorkspaceFolders.LegacySettingsFile);
        var settings = Path.Combine(folder, WorkspaceFolders.SettingsFile);
        if (File.Exists(legacySettings) && !File.Exists(settings))
        {
            File.Move(legacySettings, settings);
        }

        Hosts.HostDocsLayout.Upgrade(Path.Combine(folder, "hosts"), author: string.Empty);
        Skills.SkillsLayout.Upgrade(folder);

        var legacyPersonal = Path.Combine(folder, WorkspaceFolders.LegacyPersonalFolder);
        if (!Directory.Exists(legacyPersonal))
        {
            return;
        }

        bool IsLock(string entry) => string.Equals(Path.GetFileName(entry), LockFile, StringComparison.Ordinal);
        foreach (var source in Directory.EnumerateFileSystemEntries(legacyPersonal).Where(entry => !IsLock(entry)))
        {
            var target = Path.Combine(personal, Path.GetFileName(source));
            if (!Path.Exists(target))
            {
                MoveEntry(source, target);
            }
        }

        // Anything left had a newer copy in the application's data already, so the folder stays for the user to look at.
        if (!Directory.EnumerateFileSystemEntries(legacyPersonal).All(IsLock))
        {
            return;
        }

        Directory.Delete(legacyPersonal, recursive: true);
    }

    // The application's data may be on another disk than the workspace, where a folder can't simply be renamed across.
    private static void MoveEntry(string source, string target)
    {
        if (File.Exists(source))
        {
            File.Copy(source, target);
            File.Delete(source);
            return;
        }

        Directory.CreateDirectory(target);
        foreach (var entry in Directory.EnumerateFileSystemEntries(source))
        {
            MoveEntry(entry, Path.Combine(target, Path.GetFileName(entry)));
        }

        Directory.Delete(source);
    }

    private static void CreatePrivateFolder(string path)
    {
        if (Directory.Exists(path))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path);
            return;
        }

        Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static (WorkspaceSettings Settings, IReadOnlyList<string> Problems) ReadValidSettings(string path)
    {
        var settings = ReadSettings(path);
        var problems = settings.Connections.Select(connection => connection.Problem()).OfType<string>().Select(problem => $"{WorkspaceFolders.SettingsFile}: {problem} It was left out.").ToList();
        return (settings with { Connections = [.. settings.Connections.Where(connection => connection.Problem() is null)] }, problems);
    }

    private static WorkspaceSettings ReadSettings(string path)
    {
        if (!File.Exists(path))
        {
            return new WorkspaceSettings();
        }

        try
        {
            return JsonSerializer.Deserialize(File.ReadAllBytes(path), WorkspaceJsonContext.Default.WorkspaceSettings) ?? new WorkspaceSettings();
        }
        catch (JsonException exception)
        {
            throw new WorkspaceException($"{WorkspaceFolders.SettingsFile} couldn't be read: {exception.Message}", exception);
        }
    }

    private static FileStream? TryLock(string personal)
    {
        try
        {
            return new FileStream(Path.Combine(personal, LockFile), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException)
        {
            return null;
        }
    }
}
