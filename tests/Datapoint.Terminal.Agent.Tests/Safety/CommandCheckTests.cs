using Datapoint.Terminal.Agent.Safety;

namespace Datapoint.Terminal.Agent.Tests.Safety;

public sealed class CommandCheckTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ls\nrm -rf /")]
    [InlineData("ls\r")]
    [InlineData("echo \u001b]0;title\u0007")]
    [InlineData("echo \u0003")]
    public void Rejects_commands_that_are_not_one_plain_line(string command) => Assert.NotNull(CommandCheck.Problem(command));

    [Fact]
    public void Rejects_very_long_commands() => Assert.NotNull(CommandCheck.Problem(new string('a', 5000)));

    [Theory]
    [InlineData("ls -la")]
    [InlineData("printf 'a\\tb'")]
    [InlineData("echo café ✓")]
    public void Accepts_a_plain_line(string command) => Assert.Null(CommandCheck.Problem(command));
}
