using System.Text.Json;

namespace Datapoint.Terminal.Agent.Workspaces.Sync;

// Finds the folders that sync services upload from (product decision 90), from where each service keeps them by default or
// says it does. A service set up somewhere unusual can be missed; the warning is a courtesy, not a guarantee.
public static class SyncFolders
{
    private static readonly (string Prefix, string Service)[] s_cloudStorage =
    [
        ("OneDrive", "OneDrive"),
        ("Dropbox", "Dropbox"),
        ("GoogleDrive", "Google Drive"),
        ("Box", "Box"),
        ("iCloud", "iCloud Drive"),
    ];

    private static readonly string[] s_oneDriveVariables = ["OneDrive", "OneDriveConsumer", "OneDriveCommercial"];

    private static readonly string[] s_dropboxAccounts = ["personal", "business"];

    public static IReadOnlyList<SyncFolder> Find(string home, Func<string, string?> environment, IEnumerable<(string Root, string Label)> volumes)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(volumes);
        var found = new List<SyncFolder>();
        AddMac(found, home);
        AddHomeFolders(found, home);
        AddDropbox(found, [Path.Combine(home, ".dropbox"), .. AppData(environment, "Dropbox")]);
        foreach (var name in s_oneDriveVariables)
        {
            Add(found, "OneDrive", environment(name));
        }

        // Google Drive for desktop on Windows shows the drive as a volume of its own, labelled with its name.
        foreach (var (root, label) in volumes.Where(volume => string.Equals(volume.Label, "Google Drive", StringComparison.OrdinalIgnoreCase)))
        {
            Add(found, "Google Drive", root);
        }

        return found;
    }

    // The sync folder holding the workspace, comparing real paths too, since a sync folder is often reached through a link
    // (macOS puts ~/OneDrive… links to ~/Library/CloudStorage).
    public static SyncFolder? Containing(string folder, IEnumerable<SyncFolder> folders)
    {
        ArgumentNullException.ThrowIfNull(folders);
        string[] paths = [folder, RealPath(folder)];
        return folders.FirstOrDefault(sync => paths.Any(path =>
            WorkspaceFolders.IsSameOrInside(path, sync.Path) || WorkspaceFolders.IsSameOrInside(path, RealPath(sync.Path))));
    }

    public static IReadOnlyList<SyncFolder> ForThisMachine() =>
        Find(WorkspaceFolders.Home, Environment.GetEnvironmentVariable, Volumes());

    private static void AddMac(List<SyncFolder> found, string home)
    {
        Add(found, "iCloud Drive", Path.Combine(home, "Library", "Mobile Documents"));
        var storage = Path.Combine(home, "Library", "CloudStorage");
        foreach (var folder in Folders(storage))
        {
            var name = Path.GetFileName(folder);
            var service = s_cloudStorage.FirstOrDefault(item => name.StartsWith(item.Prefix, StringComparison.OrdinalIgnoreCase)).Service;
            Add(found, service ?? name, folder);
        }
    }

    private static void AddHomeFolders(List<SyncFolder> found, string home)
    {
        Add(found, "Dropbox", Path.Combine(home, "Dropbox"));
        Add(found, "iCloud Drive", Path.Combine(home, "iCloudDrive"));
        Add(found, "Google Drive", Path.Combine(home, "Google Drive"));
        foreach (var folder in Folders(home).Where(folder => Path.GetFileName(folder).StartsWith("OneDrive", StringComparison.OrdinalIgnoreCase)))
        {
            Add(found, "OneDrive", folder);
        }
    }

    // Dropbox records where each account's folder is in info.json.
    private static void AddDropbox(List<SyncFolder> found, IEnumerable<string> settingsFolders)
    {
        foreach (var file in settingsFolders.Select(folder => Path.Combine(folder, "info.json")).Where(File.Exists))
        {
            foreach (var path in DropboxPaths(file))
            {
                Add(found, "Dropbox", path);
            }
        }
    }

    private static List<string> DropboxPaths(string file)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            return [.. s_dropboxAccounts
                .Select(account => document.RootElement.TryGetProperty(account, out var item) && item.TryGetProperty("path", out var path) && path.ValueKind == JsonValueKind.String ? path.GetString() : null)
                .OfType<string>()];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return [];
        }
    }

    private static IEnumerable<string> AppData(Func<string, string?> environment, string name) =>
        new[] { environment("APPDATA"), environment("LOCALAPPDATA") }.OfType<string>().Select(folder => Path.Combine(folder, name));

    private static void Add(List<SyncFolder> found, string service, string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || !Directory.Exists(path)
            || found.Any(item => WorkspaceFolders.AreSame(item.Path, path)))
        {
            return;
        }

        found.Add(new SyncFolder(service, WorkspaceFolders.Normalize(path)));
    }

    private static string[] Folders(string folder)
    {
        try
        {
            return Directory.Exists(folder) ? Directory.GetDirectories(folder) : [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static List<(string Root, string Label)> Volumes()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        var volumes = new List<(string Root, string Label)>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                volumes.Add((drive.RootDirectory.FullName, drive.VolumeLabel));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }

        return volumes;
    }

    // The path with every link along it followed, or the path itself when that fails.
    public static string RealPath(string path)
    {
        try
        {
            var full = WorkspaceFolders.Normalize(path);
            var current = Path.GetPathRoot(full) ?? string.Empty;
            foreach (var name in full[current.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, name);
                if (new DirectoryInfo(current).ResolveLinkTarget(returnFinalTarget: true) is { } target)
                {
                    current = target.FullName;
                }
            }

            return current;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return path;
        }
    }
}
