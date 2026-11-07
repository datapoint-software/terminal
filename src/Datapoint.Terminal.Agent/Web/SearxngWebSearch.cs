using System.Text.Json;

namespace Datapoint.Terminal.Agent.Web;

// A SearXNG server the user runs or chose, with its JSON format turned on: https://docs.searxng.org/dev/search_api.html.
// It's the user's own server, so it may be on their network; the address guard is for pages the agent picks.
public sealed class SearxngWebSearch(Uri server, HttpMessageHandler? handler = null) : HttpWebSearch(handler)
{
    private readonly Uri _search = new(server.AbsoluteUri.EndsWith('/') ? server : new Uri(server.AbsoluteUri + "/"), "search");

    public override string Name => $"SearXNG ({server.Host})";

    protected override HttpRequestMessage CreateRequest(string query)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_search, $"?q={Uri.EscapeDataString(query)}&format=json"));
        request.Headers.Accept.ParseAdd("application/json");
        return request;
    }

    protected override WebSearchResults Parse(JsonElement root) =>
        new(root.TryGetProperty("results", out var results) ? Results(results, "content") : []);
}
