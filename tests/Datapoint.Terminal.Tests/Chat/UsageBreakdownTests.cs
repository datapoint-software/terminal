using Datapoint.Terminal.Agent.Usage;
using Datapoint.Terminal.Chat;

namespace Datapoint.Terminal.Tests.Chat;

public sealed class UsageBreakdownTests
{
    [Fact]
    public void Says_how_many_requests_and_what_each_part_of_the_input_cost()
    {
        var meter = new UsageMeter { Price = new TokenPrice(4m, 20m) { CachedInputPerMillion = 0.2m, CacheWritePerMillion = 5m } };
        meter.Record("web1", new TokenUsage(20_000, 500) { CacheRead = 18_000, CacheWrite = 1_500 });
        meter.Record("web1", new TokenUsage(22_000, 500) { CacheRead = 20_000, CacheWrite = 1_800 });

        var lines = UsageText.Breakdown(meter.Session).Split('\n');

        Assert.Equal(5, lines.Length);
        Assert.Matches(@"^Requests to the model: 2, about 21k input tokens each$", lines[0]);
        Assert.Matches(@"^New input: 700 tokens · US\$0[.,]003$", lines[1]);
        Assert.Matches(@"^Read from the cache: 38k tokens · US\$0[.,]008$", lines[2]);
        Assert.Matches(@"^Written to the cache: 3[.,]3k tokens · US\$0[.,]017$", lines[3]);
        Assert.Matches(@"^Output: 1k tokens · US\$0[.,]020$", lines[4]);
    }

    [Fact]
    public void Leaves_out_prices_it_doesnt_know_and_parts_that_didnt_happen()
    {
        var meter = new UsageMeter();
        meter.Record("web1", 1_000, 10);

        Assert.Equal(["Requests to the model: 1, about 1k input tokens each", "New input: 1k tokens", "Output: 10 tokens"],
            UsageText.Breakdown(meter.Session).Split('\n'), StringComparer.Ordinal);
    }
}
