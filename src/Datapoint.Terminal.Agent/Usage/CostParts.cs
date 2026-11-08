namespace Datapoint.Terminal.Agent.Usage;

// What a request cost, split by what the provider charged for, so the user can see which part to save on: input sent for the
// first time, input read from or written to the prompt cache, and the model's output.
public sealed record CostParts(decimal NewInput, decimal CacheRead, decimal CacheWrite, decimal Output)
{
    public static CostParts Zero { get; } = new(0m, 0m, 0m, 0m);

    public decimal Total => NewInput + CacheRead + CacheWrite + Output;

    public CostParts Add(CostParts other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new(NewInput + other.NewInput, CacheRead + other.CacheRead, CacheWrite + other.CacheWrite, Output + other.Output);
    }
}
