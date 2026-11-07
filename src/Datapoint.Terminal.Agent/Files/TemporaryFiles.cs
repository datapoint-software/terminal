using Datapoint.Terminal.Agent.Workspaces;

namespace Datapoint.Terminal.Agent.Files;

// Product decision 101: a file in a workspace is written whole to a temporary file, then moved into place, so neither a crash
// nor a sync service ever sees half of it. The temporary file is kept outside the workspace, in the application's data, when
// that's on the same disk, so the move is a rename; otherwise beside the file, under a name starting "~$", which OneDrive and
// Dropbox leave alone as they do Office's lock files.
public static class TemporaryFiles
{
    public const string SyncSkippedPrefix = "~$";

    public static string Folder => Path.Combine(Path.GetDirectoryName(WorkspaceFolders.PersonalRoot)!, "temp");

    public static string For(string target) => For(target, Folder);

    public static string For(string target, string folder)
    {
        ArgumentNullException.ThrowIfNull(target);
        var name = Path.GetFileName(target) + "." + Guid.NewGuid().ToString("N")[..12] + ".tmp";
        var outside = Path.GetFullPath(folder);
        if (IsSameDisk(outside, Path.GetFullPath(target)) && TryCreate(outside))
        {
            return Path.Combine(outside, name);
        }

        return Path.Combine(Path.GetDirectoryName(Path.GetFullPath(target))!, SyncSkippedPrefix + name);
    }

    // The disk a path is on is the mount point with the longest matching prefix, which holds on Windows and Unix alike.
    public static bool IsSameDisk(string first, string second)
    {
        try
        {
            var roots = DriveInfo.GetDrives().Select(drive => drive.RootDirectory.FullName).ToList();
            return string.Equals(MountOf(first, roots), MountOf(second, roots), StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string? MountOf(string path, IReadOnlyList<string> roots) => roots
        .Where(root => path.StartsWith(root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, root, StringComparison.OrdinalIgnoreCase))
        .MaxBy(root => root.Length);

    private static bool TryCreate(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
