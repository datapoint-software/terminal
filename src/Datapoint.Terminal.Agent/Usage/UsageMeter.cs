namespace Datapoint.Terminal.Agent.Usage;

// Product decision 49: what the agent spent in this window's session, in all and per terminal, from the token counts each
// provider reports. Each request is priced when it's made, so a later price change doesn't rewrite the past.
public sealed class UsageMeter
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, UsageTotal> _terminals = new(StringComparer.Ordinal);
    private UsageTotal _session = UsageTotal.Empty;

    // Product decision 142: the same since the conversation last started over with New Conversation, so one task's cost can
    // be read on its own. The spending limit stays on the session, so starting over doesn't lift it.
    private readonly Dictionary<string, UsageTotal> _conversationTerminals = new(StringComparer.Ordinal);
    private UsageTotal _conversation = UsageTotal.Empty;

    public UsageTotal Conversation
    {
        get
        {
            lock (_lock)
            {
                return _conversation;
            }
        }
    }

    public IReadOnlyDictionary<string, UsageTotal> ConversationTerminals
    {
        get
        {
            lock (_lock)
            {
                return new Dictionary<string, UsageTotal>(_conversationTerminals, StringComparer.Ordinal);
            }
        }
    }

    public void ForgetConversation()
    {
        lock (_lock)
        {
            _conversation = UsageTotal.Empty;
            _conversationTerminals.Clear();
        }
    }

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

    // How much of the model's context the conversation fills: the latest request's input and its reply, which the next request
    // carries. Estimated from the conversation's text when it starts, or starts over, until a reply reports it (product
    // decisions 126 and 140).
    public long? ContextTokens
    {
        get
        {
            lock (_lock)
            {
                return _contextTokens;
            }
        }
    }

    private long? _contextTokens;

    public void NoteContext(TokenUsage? usage)
    {
        if (usage is null)
        {
            return;
        }

        lock (_lock)
        {
            _contextTokens = usage.Input + usage.Output;
            _isContextEstimated = false;
        }
    }

    // A conversation that started over has no reported size until its first reply, so it's estimated until then.
    public void EstimateContext(long tokens)
    {
        lock (_lock)
        {
            _contextTokens = tokens;
            _isContextEstimated = true;
        }
    }

    private bool _isContextEstimated;

    // Whether ContextTokens is an estimate rather than what a reply reported.
    public bool IsContextEstimated
    {
        get
        {
            lock (_lock)
            {
                return _isContextEstimated;
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
        var parts = usage is null ? CostParts.Zero : Price?.PartsOf(usage);
        lock (_lock)
        {
            _session = _session.Add(usage, parts);
            _conversation = _conversation.Add(usage, parts);
            if (terminal is not null)
            {
                _terminals[terminal] = _terminals.GetValueOrDefault(terminal, UsageTotal.Empty).Add(usage, parts);
                _conversationTerminals[terminal] = _conversationTerminals.GetValueOrDefault(terminal, UsageTotal.Empty).Add(usage, parts);
            }
        }
    }
}
