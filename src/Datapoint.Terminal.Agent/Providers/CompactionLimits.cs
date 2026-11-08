namespace Datapoint.Terminal.Agent.Providers;

// Product decision 133: how large the conversation may grow before the agent summarizes it and carries on from the summary.
// Every reply resends the whole conversation, so a long task costs more with each step; past this size, a summary is cheaper
// than what it replaces. The user can set their own size, or 0 for never. A model with a small window is summarized before it
// fills, since a request past the window fails.
public static class CompactionLimits
{
    public const int Default = 100_000;
    public const int Minimum = 20_000;
    public const int Never = 0;

    // Null when the conversation is never summarized.
    public static int? For(int? contextWindowTokens, int? chosen)
    {
        if (chosen == Never)
        {
            return null;
        }

        var limit = chosen is { } value ? Math.Max(value, Minimum) : Default;
        return contextWindowTokens is { } window and > 0 ? Math.Min(limit, Math.Max(window * 3 / 4, Minimum / 2)) : limit;
    }
}
