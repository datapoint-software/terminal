namespace Datapoint.Terminal.Agent.Usage;

// What a provider charges, in US dollars per million tokens: from the app's list of known models (product decision 126) or as
// the user gave it. Tokens read from or written to the prompt cache cost the input price unless their own price is known.
// Some models charge more for a request whose prompt is longer than a threshold; that whole request pays LongPrompt.
public sealed record TokenPrice(decimal InputPerMillion, decimal OutputPerMillion)
{
    public decimal? CachedInputPerMillion { get; init; }

    public decimal? CacheWritePerMillion { get; init; }

    // The prompt length, in input tokens, above which LongPrompt applies.
    public long? LongPromptAbove { get; init; }

    public TokenPrice? LongPrompt { get; init; }

    public decimal CostOf(long input, long output) => CostOf(new TokenUsage(input, output));

    public decimal CostOf(TokenUsage usage) => PartsOf(usage).Total;

    public CostParts PartsOf(TokenUsage usage)
    {
        ArgumentNullException.ThrowIfNull(usage);
        if (LongPrompt is { } longer && usage.Input > LongPromptAbove)
        {
            return longer.PartsOf(usage);
        }

        var uncached = Math.Max(0, usage.Input - usage.CacheRead - usage.CacheWrite);
        return new CostParts(
            uncached * InputPerMillion / 1_000_000m,
            usage.CacheRead * (CachedInputPerMillion ?? InputPerMillion) / 1_000_000m,
            usage.CacheWrite * (CacheWritePerMillion ?? InputPerMillion) / 1_000_000m,
            usage.Output * OutputPerMillion / 1_000_000m);
    }
}
