using System.Diagnostics;
using Datapoint.Terminal.Agent.Projects;
using Datapoint.Terminal.Agent.Projects.Git;

namespace Datapoint.Terminal.Agent.Tests.Projects.Git;

// Compared with Git itself, run only in the temporary repositories these tests create.
public sealed class GitRepositoryTests : IDisposable
{
    private static readonly string[] s_rootRules =
    [
        "# comment", "*.log", "!keep.log", "build/", "/root-only.txt", "docs/*.md", "**/cache", "logs/**", "a/**/z", "foo?bar",
        "[abc]x.txt", "[!d]y.txt", "\\#hash", "spaced  ", "\\!bang", ".terminal/",
    ];

    private static readonly string[] s_paths =
    [
        "app.log", "keep.log", "sub/x.log", "sub/important.log", "build/out.o", "build/keep.log", "src/build/x", "root-only.txt",
        "sub/root-only.txt", "docs/a.md", "docs/deep/a.md", "x/cache/y", "cache", "logs/a/b", "a/z", "a/b/c/z", "fooXbar", "ax.txt",
        "dx.txt", "ey.txt", "dy.txt", "#hash", "spaced", "!bang", ".terminal/lock", "sub/.terminal/lock", "sub/local/f",
        "secret.txt", "f.swp", "plain.txt",
    ];

    private readonly string _home = Directory.CreateTempSubdirectory("datapoint-git-").FullName;

    private string Repository => Path.Combine(_home, "repo");

    public void Dispose() => Directory.Delete(_home, recursive: true);

    private static bool HasGit()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("git", "--version") { RedirectStandardOutput = true, UseShellExecute = false });
            process!.WaitForExit();
            return process.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private string Git(string folder, string input, params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = folder, UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in (string[])["-c", "user.name=Test", "-c", "user.email=test@example.com", "-c", "commit.gpgsign=false", .. arguments])
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["HOME"] = _home;
        start.Environment["XDG_CONFIG_HOME"] = Path.Combine(_home, ".config");
        start.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        using var process = Process.Start(start)!;
        process.StandardInput.Write(input);
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }

    private void Write(string relative, string text = "")
    {
        var path = Path.Combine(Repository, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private void CreateRepository()
    {
        Directory.CreateDirectory(Repository);
        Git(Repository, string.Empty, "init", "-q");
    }

    [Fact]
    public void Ignores_the_same_paths_as_git()
    {
        Assert.SkipUnless(HasGit(), "Git isn't installed.");
        CreateRepository();
        Write(".gitignore", string.Join('\n', s_rootRules) + "\n");
        Write("sub/.gitignore", "!important.log\nlocal/\n");
        Write(".git/info/exclude", "secret.txt\n");
        Directory.CreateDirectory(Path.Combine(_home, ".config", "git"));
        File.WriteAllText(Path.Combine(_home, ".config", "git", "ignore"), "*.swp\n");
        foreach (var path in s_paths)
        {
            Write(path);
        }

        var output = Git(Repository, string.Join('\0', s_paths) + "\0", "check-ignore", "--no-index", "-z", "--stdin");
        var expected = output.Split('\0', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        var repository = GitRepository.Find(Repository, _home)!;
        var actual = s_paths.Where(path =>
        {
            var full = Path.Combine(Repository, path.Replace('/', Path.DirectorySeparatorChar));
            return repository.IgnoreRulesFor(full, _home).IsIgnored(full, isDirectory: false);
        }).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(expected.Order(StringComparer.Ordinal), actual.Order(StringComparer.Ordinal));
        Assert.Contains("build/keep.log", actual);
        Assert.DoesNotContain("plain.txt", actual);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("3")]
    [InlineData("4")]
    public void Reads_which_folders_are_tracked_from_the_index(string? version)
    {
        Assert.SkipUnless(HasGit(), "Git isn't installed.");
        CreateRepository();
        Write("sub/.terminal/notes.md");
        Write("sub/.terminalish/x");
        Write("other/file");
        Git(Repository, string.Empty, "add", "sub/.terminal", "sub/.terminalish");
        Git(Repository, string.Empty, "add", "-N", "other/file");
        if (version is not null)
        {
            Git(Repository, string.Empty, "update-index", "--index-version", version);
        }

        var repository = GitRepository.Find(Path.Combine(Repository, "sub"), _home)!;

        Assert.True(repository.HasTrackedFilesUnder(Path.Combine(Repository, "sub", ".terminal")));
        Assert.False(repository.HasTrackedFilesUnder(Path.Combine(Repository, ".terminal")));
        Assert.True(repository.HasTrackedFilesUnder(Path.Combine(Repository, "other")));
    }

    [Fact]
    public void Finds_a_worktree_and_its_shared_exclude_file()
    {
        Assert.SkipUnless(HasGit(), "Git isn't installed.");
        CreateRepository();
        Write("readme.md");
        Git(Repository, string.Empty, "add", ".");
        Git(Repository, string.Empty, "commit", "-q", "-m", "first");
        var worktree = Path.Combine(_home, "worktree");
        Git(Repository, string.Empty, "worktree", "add", "-q", worktree);
        Write(".git/info/exclude", ".terminal/\n");

        var repository = GitRepository.Find(Path.Combine(worktree), _home)!;
        var terminal = Path.Combine(worktree, ".terminal");

        Assert.Equal(ProjectFolders.Normalize(worktree), repository.Root);
        Assert.True(repository.IgnoreRulesFor(terminal, _home).IsIgnored(terminal, isDirectory: true));
    }

    [Fact]
    public void Adds_a_line_that_git_honours_for_that_folder_only()
    {
        Assert.SkipUnless(HasGit(), "Git isn't installed.");
        CreateRepository();
        Write(".gitignore", "*.tmp");
        Write("my project[1]*/.terminal/lock");
        Write("other/.terminal/lock");
        var repository = GitRepository.Find(Path.Combine(Repository, "my project[1]*"), _home)!;

        repository.AddToGitIgnore(Path.Combine(Repository, "my project[1]*", ".terminal"));

        var output = Git(Repository, "my project[1]*/.terminal/lock\0other/.terminal/lock\0", "check-ignore", "--no-index", "-z", "--stdin");
        Assert.Equal(["my project[1]*/.terminal/lock"], output.Split('\0', StringSplitOptions.RemoveEmptyEntries));
        Assert.StartsWith("*.tmp\n# ", File.ReadAllText(Path.Combine(Repository, ".gitignore")), StringComparison.Ordinal);
    }

    [Fact]
    public void Stops_looking_at_the_home_folder()
    {
        var project = Path.Combine(_home, "plain");
        Directory.CreateDirectory(project);

        Assert.Null(GitRepository.Find(project, _home));
    }
}
