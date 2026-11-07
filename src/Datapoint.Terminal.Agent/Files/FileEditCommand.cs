namespace Datapoint.Terminal.Agent.Files;

// The command that applies an approved edit, typed into the terminal so the user sees it and sudo can ask for a password there.
// The new content was saved beside the terminal first; the target is written in place, keeping its owner and permissions.
public static class FileEditCommand
{
    public static string Build(string path, string staged, string? backup, bool useSudo)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(staged);
        var target = ShellText.QuotePath(path);
        var source = ShellText.QuotePath(staged);
        var sudo = useSudo ? "sudo " : string.Empty;
        var parts = new List<string>();
        if (backup is not null)
        {
            parts.Add("mkdir -p " + ShellText.QuotePath(backup[..backup.LastIndexOf('/')]));
            parts.Add($"{sudo}cp -p -- {target} {ShellText.QuotePath(backup)}");
        }

        parts.Add(useSudo ? $"sudo tee -- {target} < {source} > /dev/null" : $"cat -- {source} > {target}");
        parts.Add("rm -- " + source);
        return string.Join(" && ", parts);
    }

    public static string Undo(string path, string? backup, bool useSudo)
    {
        ArgumentNullException.ThrowIfNull(path);
        var sudo = useSudo ? "sudo " : string.Empty;
        return backup is null
            ? $"{sudo}rm -- {ShellText.QuotePath(path)}"
            : $"{sudo}cp -- {ShellText.QuotePath(backup)} {ShellText.QuotePath(path)}";
    }

    // Under the backups folder, by the file's full path and the time, so every version is kept.
    public static string BackupPath(string path, DateTimeOffset time)
    {
        ArgumentNullException.ThrowIfNull(path);
        var relative = path.StartsWith("~/", StringComparison.Ordinal) ? "home/" + path[2..] : path.TrimStart('/');
        return $"{HostFolders.Backups}/{relative}.{time.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture)}";
    }
}
