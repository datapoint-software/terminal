using System.Text.Json;

namespace Datapoint.Terminal.Agent.Web;

// https://api-dashboard.search.brave.com/app/documentation/web-search/get-started
public sealed class BraveWebSearch(string key, HttpMessageHandler? handler = null) : HttpWebSearch(handler)
{
    private static readonly Uri s_endpoint = new("https://api.search.brave.com/res/v1/web/search");

    public override string Name => "Brave Search";

    protected override HttpRequestMessage CreateRequest(string query)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(s_endpoint, $"?q={Uri.EscapeDataString(query)}&count={MaxResults}"));
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Add("X-Subscription-Token", key);
        return request;
    }

    protected override WebSearchResults Parse(JsonElement root) =>
        new(root.TryGetProperty("web", out var web) && web.TryGetProperty("results", out var results) ? Results(results, "description") : []);
}
