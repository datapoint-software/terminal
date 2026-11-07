namespace Datapoint.Terminal.Agent.Web;

// A search service gives Results; a provider's own search may give a written Summary with its sources instead.
public sealed record WebSearchResults(IReadOnlyList<WebSearchResult> Results, string? Summary = null, string? Problem = null)
{
    // What a search through the model provider cost, so it counts toward the spending limit.
    public Usage.TokenUsage? Usage { get; init; }

    public static WebSearchResults Failed(string problem) => new([], Problem: problem);
}
