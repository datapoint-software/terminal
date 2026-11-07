namespace Datapoint.Terminal.Settings;

// Who runs the agent's web searches (product decision 105).
public enum WebSearchService
{
    // Anthropic's or OpenAI's own search; other providers have none.
    Provider,

    Brave,

    Tavily,

    Searxng,

    Off,
}
