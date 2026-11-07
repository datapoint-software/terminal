namespace Datapoint.Terminal.Agent.Web;

// A search service gives Results; a provider's own search may give a written Summary with its sources instead.
public sealed record WebSearchResults(IReadOnlyList<WebSearchResult> Results, string? Summary = null, string? Problem = null)
{
    public static WebSearchResults Failed(string problem) => new([], Problem: problem);
}
