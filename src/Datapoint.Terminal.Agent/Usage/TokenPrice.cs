namespace Datapoint.Terminal.Agent.Usage;

// What the user told us their provider charges, in US dollars per million tokens; prices change too often to ship our own.
public sealed record TokenPrice(decimal InputPerMillion, decimal OutputPerMillion)
{
    public decimal CostOf(long input, long output) => ((input * InputPerMillion) + (output * OutputPerMillion)) / 1_000_000m;
}
