using Datapoint.Terminal.Agent.Workspaces;

namespace Datapoint.Terminal.Sidebar;

// What one folder of the workspace shows in the sidebar: folders first, then files, by name. Dotfiles are shown, except the
// workspace's personal .terminal/ folder (product decision 98) and Git's own .git/, which no one edits by hand.
public static class WorkspaceListing
{
    // A folder with more entries than this is cut short, so a huge generated folder can't stall the window.
    public const int MaxEntries = 2000;

    public static IReadOnlyList<WorkspaceEntry> List(string folder, string workspaceFolder)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        ArgumentException.ThrowIfNullOrEmpty(workspaceFolder);
        var isRoot = string.Equals(Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(workspaceFolder).TrimEnd(Path.DirectorySeparatorChar), PathComparison);
        try
        {
            return [.. new DirectoryInfo(folder).EnumerateFileSystemInfos()
                .Where(entry => !IsHidden(entry, isRoot))
                .Take(MaxEntries)
                .Select(entry => new WorkspaceEntry(entry.Name, entry.FullName, entry is DirectoryInfo))
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
            || (isRoot && string.Equals(entry.Name, WorkspaceFolders.LegacyPersonalFolder, StringComparison.Ordinal)));
}
