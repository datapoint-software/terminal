using Datapoint.Terminal.Agent.Usage;

namespace Datapoint.Terminal.Agent.Tests.Usage;

public sealed class UsageMeterTests
{
    [Fact]
    public void Prices_each_request_when_it_is_made()
    {
        var meter = new UsageMeter { Price = new TokenPrice(3m, 15m) };
        meter.Record("web1", 1_000_000, 100_000);
        meter.Price = new TokenPrice(1m, 1m);
        meter.Record("db1", 1_000_000, 0);

        Assert.Equal(new UsageTotal(2_000_000, 100_000, 5.5m, 2, 0), meter.Session);
        Assert.Equal(4.5m, meter.Terminals["web1"].Cost);
        Assert.Equal(1m, meter.Terminals["db1"].Cost);
    }

    [Fact]
    public void Leaves_the_cost_unknown_without_a_price()
    {
        var meter = new UsageMeter();
        meter.Record(null, 500, 20);

        Assert.Null(meter.Session.Cost);
        Assert.Equal(520, meter.Session.Tokens);
        Assert.Empty(meter.Terminals);
    }

    [Fact]
    public void Counts_requests_the_provider_reported_nothing_for()
    {
        var meter = new UsageMeter { Price = new TokenPrice(3m, 15m) };
        meter.Record("web1", null, null);

        Assert.Equal(new UsageTotal(0, 0, 0m, 1, 1), meter.Session);
    }

    [Fact]
    public void Is_over_budget_only_with_a_known_cost()
    {
        var meter = new UsageMeter { Budget = 0.01m };
        meter.Record(null, 1_000_000, 0);
        Assert.False(meter.IsOverBudget);

        var priced = new UsageMeter { Budget = 0.01m, Price = new TokenPrice(10m, 10m) };
        priced.Record(null, 1000, 0);
        Assert.True(priced.IsOverBudget);
    }

    [Fact]
    public void Prices_cache_reads_and_writes_at_their_own_rates()
    {
        var meter = new UsageMeter { Price = new TokenPrice(3m, 15m) { CachedInputPerMillion = 0.3m, CacheWritePerMillion = 3.75m } };

        meter.Record("web1", new TokenUsage(1_000_000, 0) { CacheRead = 800_000, CacheWrite = 100_000 });

        // 100k uncached at 3, 800k read at 0.3, 100k written at 3.75.
        Assert.Equal(0.3m + 0.24m + 0.375m, meter.Session.Cost);
        Assert.Equal(800_000, meter.Session.CachedInputTokens);
    }

    [Fact]
    public void Prices_cached_input_at_the_input_price_without_its_own()
    {
        var price = new TokenPrice(2m, 10m);

        Assert.Equal(2m, price.CostOf(new TokenUsage(1_000_000, 0) { CacheRead = 600_000 }));
    }

    [Fact]
    public void Reads_cache_counts_from_the_providers_usage()
    {
        var details = new Microsoft.Extensions.AI.UsageDetails
        {
            InputTokenCount = 2310,
            OutputTokenCount = 5,
            CachedInputTokenCount = 2000,
            AdditionalCounts = new() { ["CacheCreationInputTokens"] = 300 },
        };

        Assert.Equal(new TokenUsage(2310, 5) { CacheRead = 2000, CacheWrite = 300 }, TokenUsage.From(details));
        Assert.Null(TokenUsage.From(new Microsoft.Extensions.AI.UsageDetails()));
    }
}
