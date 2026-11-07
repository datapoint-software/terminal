namespace Datapoint.Terminal.Agent.Usage;

// What the user told us their provider charges, in US dollars per million tokens; prices change too often to ship our own.
// Tokens read from or written to the prompt cache cost the input price unless their own price is known.
public sealed record TokenPrice(decimal InputPerMillion, decimal OutputPerMillion)
{
    public decimal? CachedInputPerMillion { get; init; }

    public decimal? CacheWritePerMillion { get; init; }

    public decimal CostOf(long input, long output) => CostOf(new TokenUsage(input, output));

    public decimal CostOf(TokenUsage usage)
    {
        ArgumentNullException.ThrowIfNull(usage);
        var uncached = Math.Max(0, usage.Input - usage.CacheRead - usage.CacheWrite);
        return ((uncached * InputPerMillion)
            + (usage.CacheRead * (CachedInputPerMillion ?? InputPerMillion))
            + (usage.CacheWrite * (CacheWritePerMillion ?? InputPerMillion))
            + (usage.Output * OutputPerMillion)) / 1_000_000m;
    }
}
