namespace Datapoint.Terminal.Agent.Providers;

// Product decision 26: the most terminal output sent in one piece. By default about a tenth of the model's context window, at
// roughly four characters a token, so one long output can't crowd out the conversation; the user can set their own.
public static class OutputLimits
{
    public const int Unknown = 16_000;
    public const int Minimum = 2_000;
    public const int Maximum = 400_000;

    public static int For(int? contextWindowTokens, int? chosen) =>
        chosen is { } limit ? Math.Clamp(limit, Minimum, Maximum)
        : contextWindowTokens is { } tokens and > 0 ? (int)Math.Clamp(tokens * 4L / 10, Minimum, Maximum)
        : Unknown;
}
