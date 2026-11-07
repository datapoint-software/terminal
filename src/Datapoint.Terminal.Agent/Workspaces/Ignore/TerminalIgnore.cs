namespace Datapoint.Terminal.Agent.Workspaces.Ignore;

// `.terminalignore` files list files never sent to the provider (product decision 92). They're read from the home folder down
// to the file's own folder, or from the workspace down when it's outside the home folder, so a file in a shared folder above
// can't decide for the user.
public static class TerminalIgnore
{
    public const string FileName = ".terminalignore";

    private const int MaxFoldersSearched = 2000;
    private const int MaxFileBytes = 1024 * 1024;
    private const int MaxPatternLength = 200;
    private const int MaxPatternsPerFile = 100;

    private static bool IgnoreCase => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();

    public static bool IsIgnored(string file, string workspaceFolder, string home)
    {
        var root = WorkspaceFolders.IsSameOrInside(workspaceFolder, home) ? home : workspaceFolder;
        if (WorkspaceFolders.RelativeInside(root, file) is not { } relative)
        {
            return false;
        }

        var files = new List<IgnoreFile>();
        var folder = WorkspaceFolders.Normalize(root);
        Add(files, folder);
        foreach (var name in relative.Split('/')[..^1])
        {
            folder = Path.Combine(folder, name);
            Add(files, folder);
        }

        return new IgnoreRules(files, root).IsIgnored(file, isDirectory: false);
    }

    // Every .terminalignore that applies to the workspace: those from the top folder down to the workspace, and those inside it.
    // The search inside skips hidden folders and links, and stops at a limit, so a huge tree can't stall the conversation.
    public static IReadOnlyList<IgnoredFiles> Describe(string workspaceFolder, string home)
    {
        var workspace = WorkspaceFolders.Normalize(workspaceFolder);
        var root = WorkspaceFolders.IsSameOrInside(workspace, home) ? WorkspaceFolders.Normalize(home) : workspace;
        var folders = new List<string> { root };
        if (WorkspaceFolders.RelativeInside(root, workspace) is { } relative)
        {
            var folder = root;
            foreach (var name in relative.Split('/'))
            {
                folder = Path.Combine(folder, name);
                folders.Add(folder);
            }
        }

        folders.AddRange(FoldersInside(workspace));
        return [.. folders.Select(Read).OfType<IgnoredFiles>()];
    }

    private static IEnumerable<string> FoldersInside(string workspace)
    {
        var queue = new Queue<string>([workspace]);
        var visited = 0;
        while (queue.Count > 0 && visited < MaxFoldersSearched)
        {
            var folder = queue.Dequeue();
            visited++;
            foreach (var child in Children(folder))
            {
                yield return child;
                queue.Enqueue(child);
            }
        }
    }

    private static List<string> Children(string folder)
    {
        try
        {
            return new DirectoryInfo(folder).EnumerateDirectories()
                .Where(child => !child.Name.StartsWith('.') && child.LinkTarget is null)
                .Select(child => child.FullName)
                .ToList();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    // Only lines that are patterns, kept short: the text comes from files the agent's instructions don't vouch for.
    private static IgnoredFiles? Read(string folder)
    {
        var path = Path.Combine(folder, FileName);
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaxFileBytes)
            {
                return null;
            }

            var patterns = File.ReadLines(path)
                .Select(line => line.Trim())
                .Where(line => line.Length is > 0 and <= MaxPatternLength && IgnorePattern.Parse(line, IgnoreCase) is not null)
                .Take(MaxPatternsPerFile)
                .ToList();
            return patterns.Count == 0 ? null : new IgnoredFiles(folder, patterns);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void Add(List<IgnoreFile> files, string folder)
    {
        if (IgnoreFile.Load(Path.Combine(folder, FileName), folder, IgnoreCase) is not { } file)
        {
            return;
        }

        files.Add(file);
    }
}
