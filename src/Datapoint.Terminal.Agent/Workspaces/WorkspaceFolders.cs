namespace Datapoint.Terminal.Agent.Workspaces;

public static class WorkspaceFolders
{
    public const string SettingsFile = ".terminal.json";

    public const string PersonalFolder = ".terminal";

    public static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static string Global => Path.Combine(Home, ".datapoint-terminal");

    public static string DefaultWorkspace => Path.Combine(Global, "default");

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
        File.Exists(Path.Combine(folder, SettingsFile)) || Directory.Exists(Path.Combine(folder, PersonalFolder));

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
