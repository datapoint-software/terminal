using System.Net.Http.Json;
using System.Text.Json;

namespace Datapoint.Terminal.Agent.Web;

// https://docs.tavily.com/documentation/api-reference/endpoint/search
public sealed class TavilyWebSearch(string key, HttpMessageHandler? handler = null) : HttpWebSearch(handler)
{
    private static readonly Uri s_endpoint = new("https://api.tavily.com/search");

    public override string Name => "Tavily";

    protected override HttpRequestMessage CreateRequest(string query)
    {
        var body = new Dictionary<string, object>(StringComparer.Ordinal) { ["query"] = query, ["max_results"] = MaxResults };
        var request = new HttpRequestMessage(HttpMethod.Post, s_endpoint) { Content = JsonContent.Create(body, WebJsonContext.Default.DictionaryStringObject) };
        request.Headers.Authorization = new("Bearer", key);
        return request;
    }

    protected override WebSearchResults Parse(JsonElement root) =>
        new(root.TryGetProperty("results", out var results) ? Results(results, "content") : []);
}
