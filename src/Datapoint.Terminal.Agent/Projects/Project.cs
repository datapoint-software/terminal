using System.Text.Json;
using Datapoint.Terminal.Agent.Safety;

namespace Datapoint.Terminal.Agent.Projects;

public sealed class Project : IDisposable
{
    private const string LockFile = "lock";

    private readonly FileStream? _lock;

    private Project(string folder, ProjectSettings settings, FileStream? heldLock, bool isDefault, bool isNew)
    {
        Folder = folder;
        Settings = settings;
        _lock = heldLock;
        IsDefault = isDefault;
        IsNew = isNew;
    }

    public string Folder { get; }

    public string Name => IsDefault ? "Default project" : Path.GetFileName(Folder);

    public bool IsDefault { get; }

    public bool IsNew { get; }

    public bool IsReadOnly => _lock is null;

    public ProjectSettings Settings { get; private set; }

    public string SettingsPath => Path.Combine(Folder, ProjectFolders.SettingsFile);

    public string PersonalPath => Path.Combine(Folder, ProjectFolders.PersonalFolder);

    public static Project OpenDefault() => Open(ProjectFolders.DefaultProject, ProjectFolders.Home, create: true);

    public static Project Open(string folder) => Open(folder, ProjectFolders.Home, create: false);

    public static Project Open(string folder, string home, bool create)
    {
        var normalized = ProjectFolders.Normalize(folder);
        var isDefault = ProjectFolders.AreSame(normalized, ProjectFolders.DefaultProject);
        if (create)
        {
            Directory.CreateDirectory(normalized);
        }

        if (ProjectFolders.WhyNotAProject(normalized, home) is { } problem)
        {
            throw new ProjectException(problem);
        }

        var isNew = !ProjectFolders.IsProject(normalized);
        var personal = Path.Combine(normalized, ProjectFolders.PersonalFolder);
        try
        {
            CreatePrivateFolder(personal);
            var settings = ReadSettings(Path.Combine(normalized, ProjectFolders.SettingsFile));
            return new Project(normalized, settings, TryLock(personal), isDefault, isNew);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ProjectException($"The project in {normalized} couldn't be opened: {exception.Message}", exception);
        }
    }

    public void SaveSettings(ProjectSettings settings, SecretRedactor detector)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(detector);
        if (IsReadOnly)
        {
            throw new ProjectException("This project is open in another copy of Datapoint Terminal, so it's read-only here.");
        }

        var json = JsonSerializer.Serialize(settings, ProjectJsonContext.Default.ProjectSettings);
        if (!string.Equals(detector.Redact(json), json, StringComparison.Ordinal))
        {
            throw new ProjectException($"{ProjectFolders.SettingsFile} is shared with the project and can't hold a secret, so it wasn't saved.");
        }

        var temporary = Path.Combine(Folder, $".terminal-{Guid.NewGuid():N}.tmp");
        File.WriteAllText(temporary, json);
        File.Move(temporary, SettingsPath, overwrite: true);
        Settings = settings;
    }

    public void Dispose() => _lock?.Dispose();

    private static void CreatePrivateFolder(string path)
    {
        if (Directory.Exists(path))
        {
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path);
            return;
        }

        Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static ProjectSettings ReadSettings(string path)
    {
        if (!File.Exists(path))
        {
            return new ProjectSettings();
        }

        try
        {
            return JsonSerializer.Deserialize(File.ReadAllBytes(path), ProjectJsonContext.Default.ProjectSettings) ?? new ProjectSettings();
        }
        catch (JsonException exception)
        {
            throw new ProjectException($"{ProjectFolders.SettingsFile} couldn't be read: {exception.Message}", exception);
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
