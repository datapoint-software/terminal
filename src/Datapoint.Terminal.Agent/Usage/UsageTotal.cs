namespace Datapoint.Terminal.Agent.Usage;

// Cost is null while any of the requests counted had no price, since a partial sum would understate it.
public sealed record UsageTotal(long InputTokens, long OutputTokens, decimal? Cost, int Requests, int Unreported)
{
    public static UsageTotal Empty { get; } = new(0, 0, 0m, 0, 0);

    // The part of InputTokens read from the provider's prompt cache.
    public long CachedInputTokens { get; init; }

    public long Tokens => InputTokens + OutputTokens;

    public UsageTotal Add(TokenUsage? usage, decimal? cost) => new(
        InputTokens + (usage?.Input ?? 0),
        OutputTokens + (usage?.Output ?? 0),
        Cost is { } total && cost is { } more ? total + more : null,
        Requests + 1,
        Unreported + (usage is null ? 1 : 0))
    {
        CachedInputTokens = CachedInputTokens + (usage?.CacheRead ?? 0),
    };
}
