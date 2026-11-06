using Datapoint.Terminal.Sessions.Ssh.Config;

namespace Datapoint.Terminal.Sessions.Tests.Ssh.Config;

public sealed class SshConfigTests : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("datapoint-ssh-").FullName;

    private string SshFolder => Path.Combine(_home, ".ssh");

    public void Dispose() => Directory.Delete(_home, recursive: true);

    private SshConfig Write(string text, params (string Name, string Text)[] others)
    {
        Directory.CreateDirectory(SshFolder);
        File.WriteAllText(Path.Combine(SshFolder, "config"), text);
        foreach (var (name, content) in others)
        {
            var path = Path.Combine(SshFolder, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        return SshConfig.Load(Path.Combine(SshFolder, "config"), _home);
    }

    [Fact]
    public void Uses_the_first_value_that_applies()
    {
        var config = Write("""
            Host web
              HostName web.example.com
              User deploy
            Host *
              User root
              Port 2222
            """);

        var host = config.Resolve("web", "joao");

        Assert.Equal("web.example.com", host.HostName);
        Assert.Equal("deploy", host.User);
        Assert.Equal(2222, host.Port);
    }

    [Fact]
    public void Falls_back_to_the_alias_the_local_user_and_port_22()
    {
        var host = Write(string.Empty).Resolve("db", "joao");

        Assert.Equal("db", host.HostName);
        Assert.Equal("joao", host.User);
        Assert.Equal(22, host.Port);
    }

    [Fact]
    public void Adds_up_identity_files_and_expands_tokens()
    {
        var config = Write("""
            Host web
              IdentityFile ~/.ssh/%n_key
            Host *
              IdentityFile %d/.ssh/id_ed25519
            """);

        var host = config.Resolve("web", "joao");

        Assert.Equal([Path.Combine(_home, ".ssh", "web_key").Replace('\\', '/'), _home + "/.ssh/id_ed25519"], host.IdentityFiles.Select(file => file.Replace('\\', '/')), StringComparer.Ordinal);
    }

    [Fact]
    public void Honours_negated_patterns_and_wildcards()
    {
        var config = Write("""
            Host *.prod !bastion.prod
              User ops
            """);

        Assert.Equal("ops", config.Resolve("web.prod", "joao").User);
        Assert.Equal("joao", config.Resolve("bastion.prod", "joao").User);
    }

    [Fact]
    public void Accepts_equals_signs_quotes_and_comments()
    {
        var config = Write("""
            # a comment
            Host web
              HostName=web.example.com
              User = "deploy"  # trailing comment
              IdentityFile "~/keys/my key"
            """);

        var host = config.Resolve("web", "joao");

        Assert.Equal("web.example.com", host.HostName);
        Assert.Equal("deploy", host.User);
        Assert.EndsWith("my key", Assert.Single(host.IdentityFiles), StringComparison.Ordinal);
    }

    [Fact]
    public void Reads_included_files_in_place()
    {
        var config = Write(
            """
            Include config.d/*
            Host *
              User fallback
            """,
            ("config.d/a", "Host web\n  User deploy\n"),
            ("config.d/b", "Host db\n  Port 5022\n"));

        Assert.Equal("deploy", config.Resolve("web", "joao").User);
        Assert.Equal(5022, config.Resolve("db", "joao").Port);
        Assert.Equal("fallback", config.Resolve("db", "joao").User);
        Assert.Equal(["web", "db"], config.HostAliases(), StringComparer.Ordinal);
    }

    [Fact]
    public void Includes_only_inside_a_matching_block()
    {
        var config = Write(
            """
            Host web
              Include extra
            """,
            ("extra", "User deploy\n"));

        Assert.Equal("deploy", config.Resolve("web", "joao").User);
        Assert.Equal("joao", config.Resolve("db", "joao").User);
    }

    [Fact]
    public void Evaluates_match_against_the_host_name_so_far()
    {
        var config = Write("""
            Host web
              HostName web.internal
            Match host *.internal user joao
              ProxyJump bastion
            """);

        Assert.Equal("bastion", config.Resolve("web", "joao").Get("ProxyJump"));
        Assert.Null(config.Resolve("other", "joao").Get("ProxyJump"));
    }

    [Fact]
    public void Never_runs_match_exec()
    {
        var marker = Path.Combine(_home, "ran");
        var config = Write($"""
            Match exec "touch {marker}"
              User attacker
            """);

        var host = config.Resolve("web", "joao");

        Assert.Equal("joao", host.User);
        Assert.False(File.Exists(marker));
        Assert.Contains(host.Problems, problem => problem.Contains("Match exec", StringComparison.Ordinal));
    }

    [Fact]
    public void Lists_hosts_without_wildcards()
    {
        var config = Write("""
            Host web db
            Host *.prod !x
            Host github.com
            """);

        Assert.Equal(["web", "db", "github.com"], config.HostAliases(), StringComparer.Ordinal);
    }

    [Fact]
    public void Reads_nothing_when_there_is_no_file()
    {
        var config = SshConfig.Load(Path.Combine(_home, "missing"), _home);

        Assert.Empty(config.HostAliases());
        Assert.Empty(config.Problems);
    }

    [Fact]
    public void Stops_at_an_include_loop()
    {
        var config = Write("Include config\nHost web\n  User deploy\n");

        Assert.Equal("deploy", config.Resolve("web", "joao").User);
    }
}
