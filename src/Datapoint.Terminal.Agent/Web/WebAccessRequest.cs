namespace Datapoint.Terminal.Agent.Web;

// Target is the query or the page's address, exactly as it will be sent; Service names who receives a query.
public sealed record WebAccessRequest(WebAccessKind Kind, string Target, string Purpose)
{
    public string? Service { get; init; }

    // A page address the agent wrote itself, rather than one from a search, a page's links or the user, which could carry
    // data out; it's asked about even when the user allows the web always (IsAlwaysAllowed then says so).
    public bool IsComposed { get; init; }

    public bool IsAlwaysAllowed { get; init; }

    // Text from the terminals that the address holds.
    public string? TerminalText { get; init; }
}
