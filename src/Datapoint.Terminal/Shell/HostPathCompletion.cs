using Datapoint.Terminal.Agent.Files;

namespace Datapoint.Terminal.Shell;

// The Open a File sheet's path field: the folder being listed, and the entries that match what's typed after it.
public static class HostPathCompletion
{
    // "/etc/nginx/ngi" is the folder "/etc/nginx/" and the start of a name, "ngi".
    public static (string Folder, string Prefix) Split(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path is "~")
        {
            return ("~/", string.Empty);
        }

        var slash = path.LastIndexOf('/');
        return slash < 0 ? (string.Empty, path) : (path[..(slash + 1)], path[(slash + 1)..]);
    }

    // Folders first, then files, each by name; names that start with what's typed, ignoring case only when nothing matches exactly.
    public static IReadOnlyList<HostEntry> Matches(IReadOnlyList<HostEntry> entries, string prefix)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(prefix);
        var exact = entries.Where(entry => entry.Name.StartsWith(prefix, StringComparison.Ordinal)).ToList();
        var matches = exact.Count > 0 ? exact : [.. entries.Where(entry => entry.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))];
        if (!prefix.StartsWith('.'))
        {
            // Hidden entries only once a dot is typed, as shells complete them.
            matches = [.. matches.Where(entry => !entry.Name.StartsWith('.'))];
        }

        return [.. matches.OrderBy(entry => entry.IsFolder ? 0 : 1).ThenBy(entry => entry.Name, StringComparer.Ordinal)];
    }

    // The path once an entry is chosen: a folder to go into, or the file.
    public static string Choose(string folder, HostEntry entry)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(entry);
        return folder + entry.Name + (entry.IsFolder ? "/" : string.Empty);
    }

    // Where the sheet starts: the terminal's folder when the shell reported one, otherwise home.
    public static string Start(string? workingDirectory) =>
        workingDirectory is { Length: > 0 } folder && folder.StartsWith('/') ? folder.TrimEnd('/') + "/" : "~/";
}
