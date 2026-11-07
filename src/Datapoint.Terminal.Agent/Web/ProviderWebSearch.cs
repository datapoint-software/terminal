using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Agent.Web;

// The model provider's own web search, in a request of its own that holds only the query: nothing from the conversation or
// the terminals goes with it. The provider runs the search and bills it with the request.
public sealed class ProviderWebSearch(IChatClient client, string name) : IWebSearch
{
    private const string Instructions =
        "Search the web for the query below and list the most relevant results: for each, its title, its address and one "
        + "sentence on what it says. Use only what the search finds, not what you remember.";

    public string Name => name;

    public async Task<WebSearchResults> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var options = new ChatOptions { Tools = [new HostedWebSearchTool()], MaxOutputTokens = 2_000 };
        try
        {
            var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, Instructions + "\n\nQuery: " + query)], options, cancellationToken).ConfigureAwait(false);
            return new WebSearchResults(Sources(response), Summary: string.IsNullOrWhiteSpace(response.Text) ? null : response.Text.Trim())
            {
                Usage = Usage.TokenUsage.From(response.Usage),
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return WebSearchResults.Failed($"{name} couldn't search: {exception.Message}");
        }
    }

    // The pages the answer cites, once each.
    private static List<WebSearchResult> Sources(ChatResponse response)
    {
        var sources = new List<WebSearchResult>();
        var annotations = response.Messages.SelectMany(message => message.Contents).SelectMany(content => content.Annotations ?? []).OfType<CitationAnnotation>();
        foreach (var citation in annotations)
        {
            if (citation.Url is not { } address || !WebAddressGuard.IsWebScheme(address.Scheme) || sources.Exists(source => source.Address == address))
            {
                continue;
            }

            sources.Add(new WebSearchResult(citation.Title ?? address.Host, address, citation.Snippet));
        }

        return sources;
    }
}
