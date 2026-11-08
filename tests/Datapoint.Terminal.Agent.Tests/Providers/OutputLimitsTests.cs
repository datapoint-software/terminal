using Datapoint.Terminal.Agent.Providers;

namespace Datapoint.Terminal.Agent.Tests.Providers;

public sealed class OutputLimitsTests
{
    [Theory]
    [InlineData(null, null, OutputLimits.Unknown)]
    [InlineData(1_000_000, null, OutputLimits.DefaultMaximum)]
    [InlineData(100_000, null, 40_000)]
    [InlineData(32_000, null, 12_800)]
    [InlineData(1_000, null, OutputLimits.Minimum)]
    [InlineData(200_000, 5_000, 5_000)]
    [InlineData(1_000_000, 300_000, 300_000)]
    [InlineData(1_000_000, 900_000, OutputLimits.Maximum)]
    [InlineData(null, 10, OutputLimits.Minimum)]
    public void Follows_the_context_window_unless_the_user_chose(int? contextWindow, int? chosen, int expected)
    {
        Assert.Equal(expected, OutputLimits.For(contextWindow, chosen));
    }
}
