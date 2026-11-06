namespace Datapoint.Terminal.Agent.Projects;

public static class ProjectFolders
{
    public const string SettingsFile = ".terminal.json";

    public const string PersonalFolder = ".terminal";

    public static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static string Global => Path.Combine(Home, ".datapoint-terminal");

    public static string DefaultProject => Path.Combine(Global, "default");

    public static StringComparer Comparer => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    public static string Normalize(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        var full = Path.GetFullPath(folder);
        var root = Path.GetPathRoot(full) ?? string.Empty;
        return full.Length > root.Length ? Path.TrimEndingDirectorySeparator(full) : full;
    }

    public static bool AreSame(string first, string second) => Comparer.Equals(Normalize(first), Normalize(second));

    public static bool IsProject(string folder) =>
        File.Exists(Path.Combine(folder, SettingsFile)) || Directory.Exists(Path.Combine(folder, PersonalFolder));

    public static string? WhyNotAProject(string folder, string home)
    {
        var normalized = Normalize(folder);
        if (string.Equals(Path.GetPathRoot(normalized), normalized, StringComparison.Ordinal))
        {
            return "The root of a drive can't be a project. Choose or create a folder inside it.";
        }

        if (Comparer.Equals(normalized, Normalize(home)))
        {
            return "Your home folder can't be a project. Choose or create a folder inside it.";
        }

        return Directory.Exists(normalized) ? null : "The folder doesn't exist.";
    }
}
