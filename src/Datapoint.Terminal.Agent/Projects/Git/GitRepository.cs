using Datapoint.Terminal.Agent.Projects.Ignore;

namespace Datapoint.Terminal.Agent.Projects.Git;

// Found and read from its files only, never by running git: a repository's configuration can make Git run programs (e.g.
// core.fsmonitor), and the folder may not be trusted (engineering.md, Ignore files).
public sealed class GitRepository
{
    private GitRepository(string root, string gitFolder, string commonFolder)
    {
        Root = root;
        GitFolder = gitFolder;
        CommonFolder = commonFolder;
    }

    public string Root { get; }

    public string GitFolder { get; }

    // Shared by all of a repository's worktrees; holds info/exclude and the configuration.
    public string CommonFolder { get; }

    private static bool IgnoreCase => !OperatingSystem.IsLinux();

    // Walks up to the home folder, or, for a folder outside it, to the root of its drive.
    public static GitRepository? Find(string folder, string home)
    {
        var stop = ProjectFolders.IsSameOrInside(folder, home) ? ProjectFolders.Normalize(home) : null;
        for (var current = ProjectFolders.Normalize(folder); current is not null; current = Path.GetDirectoryName(current))
        {
            if (Open(current) is { } repository)
            {
                return repository;
            }

            if (stop is not null && ProjectFolders.AreSame(current, stop))
            {
                return null;
            }
        }

        return null;
    }

    // Lowest precedence first: the user's global file, info/exclude, then each .gitignore from the root down to the path's folder.
    public IgnoreRules IgnoreRulesFor(string path, string home)
    {
        var files = new List<IgnoreFile>();
        Add(files, GlobalExcludesFile(home), Root);
        Add(files, Path.Combine(CommonFolder, "info", "exclude"), Root);
        var folder = Root;
        Add(files, Path.Combine(folder, ".gitignore"), folder);
        if (ProjectFolders.RelativeInside(Root, path) is { } relative)
        {
            foreach (var name in relative.Split('/')[..^1])
            {
                folder = Path.Combine(folder, name);
                Add(files, Path.Combine(folder, ".gitignore"), folder);
            }
        }

        return new IgnoreRules(files, Root);
    }

    // Null when the index can't be read, e.g. in a repository with no commits yet.
    public bool? HasTrackedFilesUnder(string folder)
    {
        if (ProjectFolders.RelativeInside(Root, folder) is not { } relative)
        {
            return null;
        }

        var objectFormat = GitConfig.Read(Path.Combine(CommonFolder, "config"), "extensions", "objectformat");
        var hashLength = string.Equals(objectFormat, "sha256", StringComparison.OrdinalIgnoreCase) ? 32 : 20;
        return GitIndex.HasEntriesUnder(Path.Combine(GitFolder, "index"), relative + "/", hashLength, ProjectFolders.Comparison);
    }

    // Adds an anchored line for the folder to the .gitignore at the repository's root, which is where people look for it.
    public string AddToGitIgnore(string folder)
    {
        var relative = ProjectFolders.RelativeInside(Root, folder) ?? throw new ArgumentException("The folder isn't in the repository.", nameof(folder));
        var path = Path.Combine(Root, ".gitignore");
        var info = new FileInfo(path);
        if (info.LinkTarget is not null)
        {
            throw new IOException($"{path} is a link, so it wasn't changed.");
        }

        var existing = info.Exists ? File.ReadAllText(path) : string.Empty;
        var newline = existing.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var separator = existing.Length == 0 || existing.EndsWith('\n') ? string.Empty : newline;
        File.AppendAllText(path, $"{separator}# Personal to each user: terminal history and the agent's notes{newline}/{EscapeForGitIgnore(relative)}/{newline}");
        return path;
    }

    private static string EscapeForGitIgnore(string relative)
    {
        var text = new System.Text.StringBuilder();
        foreach (var character in relative)
        {
            text.Append(character is '\\' or '*' or '?' or '[' or ' ' ? "\\" + character : character.ToString());
        }

        return text.ToString();
    }

    private static GitRepository? Open(string folder)
    {
        var dotGit = Path.Combine(folder, ".git");
        if (Directory.Exists(dotGit))
        {
            return new GitRepository(folder, dotGit, CommonFolderOf(dotGit));
        }

        // Worktrees and submodules have a .git file that names the real folder.
        if (!File.Exists(dotGit) || ReadGitFile(dotGit) is not { } target)
        {
            return null;
        }

        var gitFolder = Path.GetFullPath(Path.Combine(folder, target));
        return Directory.Exists(gitFolder) ? new GitRepository(folder, gitFolder, CommonFolderOf(gitFolder)) : null;
    }

    private static string? ReadGitFile(string path)
    {
        try
        {
            var text = File.ReadAllText(path).Trim();
            return text.StartsWith("gitdir:", StringComparison.Ordinal) ? text["gitdir:".Length..].Trim() : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string CommonFolderOf(string gitFolder)
    {
        var file = Path.Combine(gitFolder, "commondir");
        try
        {
            return File.Exists(file) ? Path.GetFullPath(Path.Combine(gitFolder, File.ReadAllText(file).Trim())) : gitFolder;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return gitFolder;
        }
    }

    // core.excludesFile from the repository's or the user's configuration, or Git's default under ~/.config/git.
    private string GlobalExcludesFile(string home)
    {
        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } value ? value : Path.Combine(home, ".config");
        var configured = GitConfig.Read(Path.Combine(CommonFolder, "config"), "core", "excludesfile")
            ?? GitConfig.Read(Path.Combine(home, ".gitconfig"), "core", "excludesfile")
            ?? GitConfig.Read(Path.Combine(xdg, "git", "config"), "core", "excludesfile");
        return configured is null
            ? Path.Combine(xdg, "git", "ignore")
            : configured.StartsWith("~/", StringComparison.Ordinal) ? Path.Combine(home, configured[2..]) : configured;
    }

    private static void Add(List<IgnoreFile> files, string path, string folder)
    {
        if (IgnoreFile.Load(path, folder, IgnoreCase) is not { } file)
        {
            return;
        }

        files.Add(file);
    }
}
