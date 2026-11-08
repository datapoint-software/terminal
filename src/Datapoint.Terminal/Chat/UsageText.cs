using System.Globalization;
using Datapoint.Terminal.Agent.Usage;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Chat;

public static class UsageText
{
    public static string Summary(UsageTotal total, decimal? limit) => Summary(Strings.UsageSession, total, limit);

    public static string ConversationSummary(UsageTotal total) => Summary(Strings.UsageConversation, total, limit: null);

    private static string Summary(string format, UsageTotal total, decimal? limit)
    {
        ArgumentNullException.ThrowIfNull(total);
        var text = Strings.Format(format, Tokens(total.Tokens)) + Cost(total);
        if (limit is { } budget)
        {
            text += " " + Strings.Format(Strings.UsageLimit, Money(budget));
        }

        return total.Unreported > 0 ? text + " " + Strings.Format(Strings.UsageUnreported, total.Unreported) : text;
    }

    // Where the tokens and the money went: how many requests, the input sent anew, read from and written to the prompt cache,
    // and the output, each priced when the price is known. Every step of a task resends the conversation, so the number of
    // requests is what most of the cost follows.
    public static string Breakdown(UsageTotal total)
    {
        ArgumentNullException.ThrowIfNull(total);
        if (total.Requests == 0 || total.Tokens == 0)
        {
            return string.Empty;
        }

        var newInput = Math.Max(0, total.InputTokens - total.CachedInputTokens - total.CacheWriteTokens);
        var lines = new List<string>
        {
            Strings.Format(Strings.UsageRequests, total.Requests, Tokens(total.InputTokens / total.Requests)),
            Strings.Format(Strings.UsagePartNew, Tokens(newInput)) + Part(total.Parts?.NewInput),
        };
        if (total.CachedInputTokens > 0)
        {
            lines.Add(Strings.Format(Strings.UsagePartCacheRead, Tokens(total.CachedInputTokens)) + Part(total.Parts?.CacheRead));
        }

        if (total.CacheWriteTokens > 0)
        {
            lines.Add(Strings.Format(Strings.UsagePartCacheWrite, Tokens(total.CacheWriteTokens)) + Part(total.Parts?.CacheWrite));
        }

        lines.Add(Strings.Format(Strings.UsagePartOutput, Tokens(total.OutputTokens)) + Part(total.Parts?.Output));
        return string.Join('\n', lines);
    }

    private static string Part(decimal? cost) => cost is { } amount ? " · " + Money(amount) : string.Empty;

    public static string PerTerminal(IReadOnlyDictionary<string, UsageTotal> terminals) =>
        string.Join('\n', terminals.OrderBy(pair => pair.Key, StringComparer.CurrentCulture)
            .Select(pair => Strings.Format(Strings.UsageTerminal, pair.Key, Tokens(pair.Value.Tokens)) + Cost(pair.Value)));

    // 950, 12.3k, 4.1M: enough to judge spending at a glance.
    public static string Tokens(long count) => count switch
    {
        < 1000 => count.ToString("N0", CultureInfo.CurrentCulture),
        < 1_000_000 => (count / 1000.0).ToString("0.#", CultureInfo.CurrentCulture) + "k",
        _ => (count / 1_000_000.0).ToString("0.#", CultureInfo.CurrentCulture) + "M",
    };

    public static string Money(decimal amount) => "US$" + amount.ToString(amount < 1 ? "0.000" : "0.00", CultureInfo.CurrentCulture);

    private static string Cost(UsageTotal total) => total.Cost is { } cost && total.Tokens > 0 ? " " + Strings.Format(Strings.UsageCost, Money(cost)) : string.Empty;
}
