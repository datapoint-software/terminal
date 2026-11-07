namespace Datapoint.Terminal.Agent.Usage;

// Cost is null while any of the requests counted had no price, since a partial sum would understate it.
public sealed record UsageTotal(long InputTokens, long OutputTokens, decimal? Cost, int Requests, int Unreported)
{
    public static UsageTotal Empty { get; } = new(0, 0, 0m, 0, 0);

    public long Tokens => InputTokens + OutputTokens;

    public UsageTotal Add(long input, long output, decimal? cost, bool reported) => new(
        InputTokens + input,
        OutputTokens + output,
        Cost is { } total && cost is { } more ? total + more : null,
        Requests + 1,
        Unreported + (reported ? 0 : 1));
}
