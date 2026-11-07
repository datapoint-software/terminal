namespace Datapoint.Terminal.Agent.Workspaces;

public static class WorkspaceFolders
{
    public const string SettingsFile = "workspace.json";

    // Where a project kept its settings and personal data before workspaces; moved on first opening (autopilot entry 94).
    public const string LegacySettingsFile = ".terminal.json";

    public const string LegacyPersonalFolder = ".terminal";

    public static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static string Global => Path.Combine(Home, ".datapoint-terminal");

    public static string DefaultWorkspace => Path.Combine(Global, "default");

    // Personal data never goes in the workspace folder, which may be synced or committed (product decision 100).
    public static string PersonalRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create),
        "Datapoint Terminal",
        "workspaces");

    // Keyed by the folder's path, so a workspace moved elsewhere starts afresh rather than taking on another one's data
    // through anything written in the shared folder. Not a security boundary, so a plain FNV-1a hash; the folder's name is
    // kept in front so the user can tell the folders apart.
    public static string PersonalData(string root, string folder)
    {
        var normalized = Normalize(folder);
        var key = Comparer == StringComparer.OrdinalIgnoreCase ? normalized.ToUpperInvariant() : normalized;
        var hash = 14695981039346656037UL;
        foreach (var character in key)
        {
            hash = (hash ^ character) * 1099511628211UL;
        }

        var name = new string([.. Path.GetFileName(normalized).Where(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_').Take(32)]);
        return Path.Combine(root, $"{(name.Length == 0 ? "workspace" : name)}-{hash:x16}");
    }

    public static StringComparer Comparer => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    public static StringComparison Comparison => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    // The path below the folder with '/' between names, or null when it isn't inside it.
    public static string? RelativeInside(string folder, string path)
    {
        var relative = Path.GetRelativePath(Normalize(folder), Normalize(path));
        return string.Equals(relative, ".", StringComparison.Ordinal) || relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative)
            ? null
            : relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    public static bool IsSameOrInside(string path, string folder) => AreSame(path, folder) || RelativeInside(folder, path) is not null;

    public static string Normalize(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        var full = Path.GetFullPath(folder);
        var root = Path.GetPathRoot(full) ?? string.Empty;
        return full.Length > root.Length ? Path.TrimEndingDirectorySeparator(full) : full;
    }

    // Paths under the home folder are shown as ~/..., so the user's account name isn't sent with them.
    public static string Display(string path)
    {
        var home = Normalize(Home);
        return path.StartsWith(home + Path.DirectorySeparatorChar, Comparison)
            ? "~" + path[home.Length..]
            : path;
    }

    public static bool AreSame(string first, string second) => Comparer.Equals(Normalize(first), Normalize(second));

    public static bool IsWorkspace(string folder) =>
        File.Exists(Path.Combine(folder, SettingsFile)) || File.Exists(Path.Combine(folder, LegacySettingsFile)) || Directory.Exists(Path.Combine(folder, LegacyPersonalFolder));

    public static string? WhyNotAWorkspace(string folder, string home)
    {
        var normalized = Normalize(folder);
        if (string.Equals(Path.GetPathRoot(normalized), normalized, StringComparison.Ordinal))
        {
            return "The root of a drive can't be a workspace. Choose or create a folder inside it.";
        }

        if (Comparer.Equals(normalized, Normalize(home)))
        {
            return "Your home folder can't be a workspace. Choose or create a folder inside it.";
        }

        return Directory.Exists(normalized) ? null : "The folder doesn't exist.";
    }
}
