using Datapoint.Terminal.Agent.Safety;

namespace Datapoint.Terminal.Agent.Tests.Safety;

public sealed class CommandRulesTests
{
    private static readonly CommandRules s_rules = CommandRules.Parse("""
        // A comment, as the shipped file has.
        {
          "writingOptions": ["-o", "--output"],
          "groups": [
            { "commands": ["sort"], "writes": ["--compress-program*"] },
            { "commands": ["ps"], "allows": ["-o"] },
            { "commands": ["ip addr"], "exact": true },
            { "commands": ["ipconfig"], "writes": ["/release"], "windows": true },
          ]
        }
        """);

    [Fact]
    public void Reads_the_shipped_list() => Assert.Contains("docker node ls", CommandRules.BuiltIn.Commands, StringComparer.Ordinal);

    [Theory]
    [InlineData("sort", new[] { "-u" }, true)]
    [InlineData("sort", new[] { "-o", "x" }, false)]
    [InlineData("sort", new[] { "--output=x" }, false)]
    [InlineData("sort", new[] { "--compress-program=sh" }, false)]
    [InlineData("ps", new[] { "-o", "pid" }, true)]
    [InlineData("ip addr", new[] { "-4" }, true)]
    [InlineData("ip addr", new[] { "add", "10.0.0.1/24" }, false)]
    [InlineData("IPCONFIG", new[] { "/all" }, true)]
    [InlineData("ipconfig", new[] { "/RELEASE" }, false)]
    [InlineData("ipconfig", new[] { "-o" }, true)]
    public void Applies_each_groups_options(string command, string[] rest, bool permits) =>
        Assert.Equal(permits, s_rules.Find(command)!.Permits(command + " " + string.Join(' ', rest), rest));

    [Theory]
    [InlineData("""{"groups":[{"commands":["ls"]},{"commands":["ls"]}]}""")]
    [InlineData("""{"groups":[{"commands":["a b c d"]}]}""")]
    [InlineData("""{"groups":[{"commands":["docker  ps"]}]}""")]
    public void Refuses_a_list_it_cant_apply_reliably(string json) => Assert.Throws<InvalidDataException>(() => CommandRules.Parse(json));

    [Fact]
    public void Tells_apart_commands_that_differ_only_in_case() =>
        Assert.NotSame(CommandRules.BuiltIn.Find("dpkg -l"), CommandRules.BuiltIn.Find("dpkg -L"));
}
