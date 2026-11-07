using System.Net;
using System.Text.Json;

namespace Datapoint.Terminal.Agent.Web;

// A search service reached over its JSON API with the user's own key or server. Only the query is sent.
public abstract class HttpWebSearch : IWebSearch, IDisposable
{
    protected const int MaxResults = 8;

    private readonly HttpClient _client;

    protected HttpWebSearch(HttpMessageHandler? handler)
    {
        _client = new HttpClient(handler ?? new SocketsHttpHandler { UseCookies = false, PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
        {
            Timeout = TimeSpan.FromSeconds(20),
        };
    }

    public abstract string Name { get; }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public async Task<WebSearchResults> SearchAsync(string query, CancellationToken cancellationToken)
    {
        try
        {
            using var request = CreateRequest(query);
            using var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return WebSearchResults.Failed(Refusal(response.StatusCode));
            }

            var json = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (json.ConfigureAwait(false))
            {
                using var document = await JsonDocument.ParseAsync(json, cancellationToken: cancellationToken).ConfigureAwait(false);
                return Parse(document.RootElement);
            }
        }
        catch (HttpRequestException exception)
        {
            return WebSearchResults.Failed($"{Name} couldn't be reached: {exception.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return WebSearchResults.Failed($"{Name} took too long to answer.");
        }
        catch (JsonException)
        {
            return WebSearchResults.Failed($"{Name} gave an answer that couldn't be read.");
        }
    }

    protected abstract HttpRequestMessage CreateRequest(string query);

    protected abstract WebSearchResults Parse(JsonElement root);

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        _client.Dispose();
    }

    // The results under the named array, keeping only those with a web address.
    protected static IReadOnlyList<WebSearchResult> Results(JsonElement items, string snippetName)
    {
        var results = new List<WebSearchResult>();
        if (items.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        foreach (var item in items.EnumerateArray().Take(MaxResults))
        {
            if (Text(item, "url") is not { } url || !Uri.TryCreate(url, UriKind.Absolute, out var address) || !WebAddressGuard.IsWebScheme(address.Scheme))
            {
                continue;
            }

            results.Add(new WebSearchResult(Plain(Text(item, "title"), address) ?? address.Host, address, Plain(Text(item, snippetName), address)));
        }

        return results;
    }

    protected static string? Text(JsonElement item, string name) =>
        item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    // Services mark matches with HTML tags and entities.
    private static string? Plain(string? text, Uri address) =>
        string.IsNullOrWhiteSpace(text) ? null : HtmlText.ToMarkdown(text, address).Text;

    private string Refusal(HttpStatusCode status) => (int)status switch
    {
        401 or 403 => $"{Name} refused the key. Check it in Settings.",
        429 => $"{Name}'s limit was reached. Try again later.",
        _ => $"{Name} answered {(int)status}.",
    };
}
