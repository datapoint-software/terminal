using Datapoint.Terminal.Agent.Providers;

namespace Datapoint.Terminal.Agent.Tests.Providers;

public sealed class OutputLimitsTests
{
    [Theory]
    [InlineData(null, null, OutputLimits.Unknown)]
    [InlineData(200_000, null, 80_000)]
    [InlineData(32_000, null, 12_800)]
    [InlineData(1_000, null, OutputLimits.Minimum)]
    [InlineData(10_000_000, null, OutputLimits.Maximum)]
    [InlineData(200_000, 5_000, 5_000)]
    [InlineData(null, 10, OutputLimits.Minimum)]
    public void Follows_the_context_window_unless_the_user_chose(int? contextWindow, int? chosen, int expected)
    {
        Assert.Equal(expected, OutputLimits.For(contextWindow, chosen));
    }
}
