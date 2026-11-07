using Datapoint.Terminal.Agent.Workspaces.Ignore;

namespace Datapoint.Terminal.Agent.Tests.Workspaces.Ignore;

public sealed class IgnoredArgumentsTests : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("datapoint-arguments-").FullName;

    public void Dispose() => Directory.Delete(_home, recursive: true);

    private string Workspace => Path.Combine(_home, "api");

    private string? Find(string command, string? folder = null)
    {
        Directory.CreateDirectory(Workspace);
        File.WriteAllLines(Path.Combine(Workspace, TerminalIgnore.FileName), [".env", "*.pem", "secrets/"]);
        return IgnoredArguments.Find(command, folder ?? Workspace, _home, path => TerminalIgnore.IsIgnored(path, Workspace, _home));
    }

    [Theory]
    [InlineData("cat .env")]
    [InlineData("grep -n KEY ./.env | head")]
    [InlineData("less '.env'")]
    [InlineData("base64 \".env\"")]
    [InlineData("python tool.py --config=.env")]
    [InlineData("FILE=.env sh -c 'cat $FILE'")]
    [InlineData("cat secrets/db.json")]
    [InlineData("cat ~/api/.env")]
    [InlineData("echo $(cat .env)")]
    public void Finds_an_ignored_file_among_the_words(string command) => Assert.NotNull(Find(command));

    [Theory]
    [InlineData("ls -la")]
    [InlineData("cat README.md")]
    [InlineData("cat .env.example")]
    [InlineData("echo -env")]
    public void Leaves_other_commands_alone(string command) => Assert.Null(Find(command));

    [Fact]
    public void Resolves_paths_from_the_terminals_folder()
    {
        var sub = Directory.CreateDirectory(Path.Combine(Workspace, "src")).FullName;

        Assert.Equal(Path.Combine(Workspace, ".env"), Find("cat ../.env", sub));
        Assert.Equal(Path.Combine(sub, ".env"), Find("cat .env", sub));
        Assert.Null(Find("cat ../README.md", sub));
    }

    [Fact]
    public void Expands_a_glob_in_the_last_part_of_a_path()
    {
        Directory.CreateDirectory(Workspace);
        File.WriteAllText(Path.Combine(Workspace, "server.pem"), "x");

        Assert.Equal(Path.Combine(Workspace, "server.pem"), Find("cat *.pem"));
        Assert.Null(Find("cat *.md"));
    }

    [Fact]
    public void Splits_words_as_a_shell_would()
    {
        Assert.Equal(["echo", "a b", "c\"d", "e", "f"], IgnoredArguments.Words("echo 'a b' \"c\\\"d\" e;f"));
    }
}
