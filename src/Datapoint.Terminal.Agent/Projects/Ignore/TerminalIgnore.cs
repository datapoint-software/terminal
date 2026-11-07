namespace Datapoint.Terminal.Agent.Projects.Ignore;

// `.terminalignore` files list files never sent to the provider (product decision 92). They're read from the home folder down
// to the file's own folder, or from the project down when it's outside the home folder, so a file in a shared folder above
// can't decide for the user.
public static class TerminalIgnore
{
    public const string FileName = ".terminalignore";

    private static bool IgnoreCase => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();

    public static bool IsIgnored(string file, string projectFolder, string home)
    {
        var root = ProjectFolders.IsSameOrInside(projectFolder, home) ? home : projectFolder;
        if (ProjectFolders.RelativeInside(root, file) is not { } relative)
        {
            return false;
        }

        var files = new List<IgnoreFile>();
        var folder = ProjectFolders.Normalize(root);
        Add(files, folder);
        foreach (var name in relative.Split('/')[..^1])
        {
            folder = Path.Combine(folder, name);
            Add(files, folder);
        }

        return new IgnoreRules(files, root).IsIgnored(file, isDirectory: false);
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
