namespace Datapoint.Terminal.Agent.Usage;

// Cost is null while any of the requests counted had no price, since a partial sum would understate it.
public sealed record UsageTotal(long InputTokens, long OutputTokens, decimal? Cost, int Requests, int Unreported)
{
    public static UsageTotal Empty { get; } = new(0, 0, 0m, 0, 0);

    // The part of InputTokens read from the provider's prompt cache.
    public long CachedInputTokens { get; init; }

    // The part of InputTokens written to it, which Anthropic charges more for than new input.
    public long CacheWriteTokens { get; init; }

    // Cost by what was charged for; null, like Cost, once a request had no price.
    public CostParts? Parts { get; init; } = CostParts.Zero;

    public long Tokens => InputTokens + OutputTokens;

    // parts is null when the request had no price.
    public UsageTotal Add(TokenUsage? usage, CostParts? parts)
    {
        var counted = usage ?? new TokenUsage(0, 0);
        return new(InputTokens + counted.Input, OutputTokens + counted.Output, Sum(Cost, parts?.Total), Requests + 1, Unreported + (usage is null ? 1 : 0))
        {
            CachedInputTokens = CachedInputTokens + counted.CacheRead,
            CacheWriteTokens = CacheWriteTokens + counted.CacheWrite,
            Parts = Parts is { } sum && parts is { } added ? sum.Add(added) : null,
        };
    }

    private static decimal? Sum(decimal? total, decimal? more) => total is { } a && more is { } b ? a + b : null;
}
