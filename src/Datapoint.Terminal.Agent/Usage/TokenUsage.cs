using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Agent.Usage;

// One request's tokens. Input is all of it, including what was read from or written to the provider's prompt cache, which
// are priced differently.
public sealed record TokenUsage(long Input, long Output)
{
    public long CacheRead { get; init; }

    public long CacheWrite { get; init; }

    // Anthropic's adapter reports tokens written to the cache under this name; OpenAI doesn't charge for writing.
    private const string CacheWriteKey = "CacheCreationInputTokens";

    // Null when the provider reported no counts at all.
    public static TokenUsage? From(UsageDetails? details) => details is null || (details.InputTokenCount is null && details.OutputTokenCount is null)
        ? null
        : new TokenUsage(details.InputTokenCount ?? 0, details.OutputTokenCount ?? 0)
        {
            CacheRead = details.CachedInputTokenCount ?? 0,
            CacheWrite = details.AdditionalCounts?.GetValueOrDefault(CacheWriteKey) ?? 0,
        };
}
