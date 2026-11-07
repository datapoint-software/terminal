namespace Datapoint.Terminal.Agent.Usage;

// Product decision 49: what the agent spent in this window's session, in all and per terminal, from the token counts each
// provider reports. Each request is priced when it's made, so a later price change doesn't rewrite the past.
public sealed class UsageMeter
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, UsageTotal> _terminals = new(StringComparer.Ordinal);
    private UsageTotal _session = UsageTotal.Empty;

    public TokenPrice? Price { get; set; }

    // In US dollars for the session; only enforced while a price is known.
    public decimal? Budget { get; set; }

    public UsageTotal Session
    {
        get
        {
            lock (_lock)
            {
                return _session;
            }
        }
    }

    public IReadOnlyDictionary<string, UsageTotal> Terminals
    {
        get
        {
            lock (_lock)
            {
                return new Dictionary<string, UsageTotal>(_terminals, StringComparer.Ordinal);
            }
        }
    }

    public bool IsOverBudget => Budget is { } budget && Session.Cost is { } cost && cost >= budget;

    // input and output are null when the provider didn't report them; the request still counts.
    public void Record(string? terminal, long? input, long? output) =>
        Record(terminal, input is null && output is null ? null : new TokenUsage(input ?? 0, output ?? 0));

    // usage is null when the provider reported no counts; the request still counts.
    public void Record(string? terminal, TokenUsage? usage)
    {
        var cost = usage is null ? 0m : Price?.CostOf(usage);
        lock (_lock)
        {
            _session = _session.Add(usage, cost);
            if (terminal is not null)
            {
                _terminals[terminal] = _terminals.GetValueOrDefault(terminal, UsageTotal.Empty).Add(usage, cost);
            }
        }
    }
}
