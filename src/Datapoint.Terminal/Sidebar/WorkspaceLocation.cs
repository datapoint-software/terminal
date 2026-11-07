namespace Datapoint.Terminal.Sidebar;

// Where a workspace's folder is, in a few words for the switcher: the service it's shared through when the path says so,
// otherwise the path, shortened from the home folder.
public static class WorkspaceLocation
{
    // Folder names the sync clients create, in the path from the home folder; product names, so not translated.
    private static readonly (string Prefix, string Service)[] s_services =
    [
        ("OneDrive", "OneDrive"),
        ("Dropbox", "Dropbox"),
        ("GoogleDrive", "Google Drive"),
        ("Google Drive", "Google Drive"),
        ("Mobile Documents", "iCloud Drive"),
        ("iCloud Drive", "iCloud Drive"),
        ("Box", "Box"),
    ];

    public static string Describe(string folder, string home)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        ArgumentException.ThrowIfNullOrEmpty(home);
        var full = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var root = Path.GetFullPath(home).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return full;
        }

        var relative = full[(root.Length + 1)..];
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (s_services.FirstOrDefault(item => segment.StartsWith(item.Prefix, StringComparison.OrdinalIgnoreCase)).Service is { } service)
            {
                return service;
            }
        }

        return "~/" + relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    // Up to two letters for the workspace's badge: the first of its first two words, or its first two.
    public static string Initials(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var words = name.Split([' ', '-', '_', '.'], StringSplitOptions.RemoveEmptyEntries).Where(word => char.IsLetterOrDigit(word[0])).ToList();
        var letters = words.Count >= 2 ? $"{words[0][0]}{words[1][0]}" : new string([.. (words.FirstOrDefault() ?? "?").Where(char.IsLetterOrDigit).Take(2)]);
        return letters.Length == 0 ? "?" : letters.ToUpperInvariant();
    }
}
