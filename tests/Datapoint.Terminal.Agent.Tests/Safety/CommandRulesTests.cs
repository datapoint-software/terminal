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

    public static TheoryData<string> ListedCommands => [.. CommandRules.BuiltIn.Commands];

    public static TheoryData<string, string> ListedWritingOptions =>
    [
        .. CommandRules.BuiltIn.Commands.SelectMany(command => CommandRules.BuiltIn.Find(command)!.Writes
            .Select(option => (command, option.TrimEnd('*')))),
    ];

    public static TheoryData<string> ExactCommands => [.. CommandRules.BuiltIn.Commands.Where(command => CommandRules.BuiltIn.Find(command)!.IsExact)];

    [Theory]
    [MemberData(nameof(ListedCommands))]
    public void Every_listed_command_counts_as_read_only(string command) =>
        Assert.Equal(CommandRisk.ReadOnly, CommandClassifier.Classify(command));

    [Theory]
    [MemberData(nameof(ListedWritingOptions))]
    public void Every_listed_command_with_a_writing_option_counts_as_a_change(string command, string option) =>
        Assert.Equal(CommandRisk.Changes, CommandClassifier.Classify(command + " " + option + " x"));

    [Theory]
    [MemberData(nameof(ExactCommands))]
    public void An_exact_command_followed_by_another_word_counts_as_a_change(string command) =>
        Assert.Equal(CommandRisk.Changes, CommandClassifier.Classify(command + " delete x"));

    [Fact]
    public void Tells_apart_commands_that_differ_only_in_case() =>
        Assert.NotSame(CommandRules.BuiltIn.Find("dpkg -l"), CommandRules.BuiltIn.Find("dpkg -L"));
}
