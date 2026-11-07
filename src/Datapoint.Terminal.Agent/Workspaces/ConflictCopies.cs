namespace Datapoint.Terminal.Agent.Workspaces;

// Copies a sync service leaves beside a file that changed in two places at once (product decision 101), named after it: Dropbox's
// "SKILL (Ana's conflicted copy 2026-11-07).md", OneDrive's "SKILL-LAPTOP.md", iCloud Drive's "SKILL 2.md", Syncthing's
// "SKILL.sync-conflict-….md", and the "[Conflict] SKILL.md" some others use. Next to a SKILL.md or TERMINAL.md nothing else is
// expected to be named that way, so any such file counts.
public static class ConflictCopies
{
    private const string TemporaryPrefix = "~$";

    public static IReadOnlyList<string> Find(string file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var folder = Path.GetDirectoryName(file)!;
        try
        {
            return Directory.Exists(folder)
                ? [.. Directory.EnumerateFiles(folder).Where(path => !string.Equals(path, file, StringComparison.Ordinal) && IsCopyOf(Path.GetFileName(path), Path.GetFileName(file))).Order(StringComparer.Ordinal)]
                : [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public static bool IsCopyOf(string name, string original)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(original);
        var extension = Path.GetExtension(original);
        var stem = Path.GetFileNameWithoutExtension(original);
        if (name.StartsWith(TemporaryPrefix, StringComparison.Ordinal) || !name.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, original, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var copy = name[..^extension.Length];
        return (copy.Length > stem.Length && copy.StartsWith(stem, StringComparison.OrdinalIgnoreCase) && copy[stem.Length] is ' ' or '-' or '_' or '.' or '(')
            || (copy.StartsWith('[') && copy.EndsWith("] " + stem, StringComparison.OrdinalIgnoreCase));
    }
}
