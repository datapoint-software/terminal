using System.Globalization;
using Datapoint.Terminal.Agent.Usage;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Chat;

public static class UsageText
{
    public static string Summary(UsageTotal total, decimal? limit)
    {
        var text = Strings.Format(Strings.UsageSession, Tokens(total.Tokens)) + Cost(total);
        if (limit is { } budget)
        {
            text += " " + Strings.Format(Strings.UsageLimit, Money(budget));
        }

        return total.Unreported > 0 ? text + " " + Strings.Format(Strings.UsageUnreported, total.Unreported) : text;
    }

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
