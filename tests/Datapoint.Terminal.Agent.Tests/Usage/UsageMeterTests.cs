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
}
