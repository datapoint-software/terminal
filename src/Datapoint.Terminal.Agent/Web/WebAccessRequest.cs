namespace Datapoint.Terminal.Agent.Web;

// Target is the query or the page's address, exactly as it will be sent; Service names who receives a query.
public sealed record WebAccessRequest(WebAccessKind Kind, string Target, string Purpose)
{
    public string? Service { get; init; }
}
