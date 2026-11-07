using Datapoint.Terminal.Agent.Projects;

namespace Datapoint.Terminal.Sidebar;

// What one folder of the project shows in the sidebar: folders first, then files, by name. Dotfiles are shown, except the
// project's personal .terminal/ folder (product decision 98) and Git's own .git/, which no one edits by hand.
public static class ProjectListing
{
    // A folder with more entries than this is cut short, so a huge generated folder can't stall the window.
    public const int MaxEntries = 2000;

    public static IReadOnlyList<ProjectEntry> List(string folder, string projectFolder)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        ArgumentException.ThrowIfNullOrEmpty(projectFolder);
        var isRoot = string.Equals(Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(projectFolder).TrimEnd(Path.DirectorySeparatorChar), PathComparison);
        try
        {
            return [.. new DirectoryInfo(folder).EnumerateFileSystemInfos()
                .Where(entry => !IsHidden(entry, isRoot))
                .Take(MaxEntries)
                .Select(entry => new ProjectEntry(entry.Name, entry.FullName, entry is DirectoryInfo))
                .OrderBy(entry => entry.IsFolder ? 0 : 1)
                .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return [];
        }
    }

    private static StringComparison PathComparison => OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    private static bool IsHidden(FileSystemInfo entry, bool isRoot) =>
        entry is DirectoryInfo && (string.Equals(entry.Name, ".git", StringComparison.Ordinal)
            || (isRoot && string.Equals(entry.Name, ProjectFolders.PersonalFolder, StringComparison.Ordinal)));
}
