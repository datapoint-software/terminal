using System.Net;
using System.Text;
using Datapoint.Terminal.Agent.Web;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Agent.Tests.Web;

public sealed class WebSearchTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Brave_sends_the_key_in_a_header_and_reads_its_results()
    {
        var handler = new Handler("""{"web":{"results":[{"title":"rsync <strong>flags</strong>","url":"https://example.org/rsync","description":"Use &lt;-a&gt;."},{"title":"x","url":"javascript:alert(1)"}]}}""");
        using var search = new BraveWebSearch("brave-key", handler);

        var results = await search.SearchAsync("rsync flags", Token);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("brave-key", request.Headers.GetValues("X-Subscription-Token").Single());
        Assert.DoesNotContain("brave-key", request.RequestUri!.AbsoluteUri, StringComparison.Ordinal);
        Assert.Contains("?q=rsync%20flags&", request.RequestUri.AbsoluteUri, StringComparison.Ordinal);
        var result = Assert.Single(results.Results);
        Assert.Equal("rsync flags", result.Title);
        Assert.Equal("Use <-a>.", result.Snippet);
    }

    [Fact]
    public async Task Tavily_posts_the_query_with_a_bearer_key()
    {
        var handler = new Handler("""{"results":[{"title":"Guide","url":"https://example.org/guide","content":"Steps."}]}""");
        using var search = new TavilyWebSearch("tvly-key", handler);

        var results = await search.SearchAsync("nginx reload", Token);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("Bearer tvly-key", request.Headers.Authorization?.ToString());
        Assert.Contains("\"query\":\"nginx reload\"", handler.Bodies.Single(), StringComparison.Ordinal);
        Assert.Equal("https://example.org/guide", Assert.Single(results.Results).Address.AbsoluteUri);
    }

    [Theory]
    [InlineData("http://searx.lan:8080")]
    [InlineData("http://searx.lan:8080/")]
    public async Task Searxng_asks_its_server_for_json(string server)
    {
        var handler = new Handler("""{"results":[{"title":"Docs","url":"https://example.org/docs","content":"Read me."}]}""");
        using var search = new SearxngWebSearch(new Uri(server), handler);

        await search.SearchAsync("systemd timers", Token);

        Assert.StartsWith("http://searx.lan:8080/search?q=systemd%20timers&format=json", Assert.Single(handler.Requests).RequestUri!.AbsoluteUri, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "refused the key")]
    [InlineData(HttpStatusCode.TooManyRequests, "limit was reached")]
    public async Task Says_why_a_search_failed(HttpStatusCode status, string reason)
    {
        using var search = new BraveWebSearch("key", new Handler("{}", status));

        var results = await search.SearchAsync("query", Token);

        Assert.Contains(reason, results.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_provider_search_sends_only_the_query_and_lists_what_it_cites()
    {
        var answer = new TextContent("Use -a.")
        {
            Annotations = [new CitationAnnotation { Title = "rsync(1)", Url = new Uri("https://example.org/rsync"), Snippet = "archive mode" }],
        };
        var client = new OneShotClient(new ChatResponse(new ChatMessage(ChatRole.Assistant, [answer])) { Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5 } });
        var search = new ProviderWebSearch(client, "Anthropic");

        var results = await search.SearchAsync("rsync archive", Token);

        var message = Assert.Single(client.Messages);
        Assert.EndsWith("Query: rsync archive", message.Text, StringComparison.Ordinal);
        Assert.IsType<HostedWebSearchTool>(Assert.Single(client.Options!.Tools!));
        Assert.Equal("Use -a.", results.Summary);
        Assert.Equal("rsync(1)", Assert.Single(results.Results).Title);
        Assert.Equal(10, results.Usage?.Input);
    }

    private sealed class Handler(string json, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class OneShotClient(ChatResponse response) : IChatClient
    {
        public List<ChatMessage> Messages { get; } = [];

        public ChatOptions? Options { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Messages.AddRange(messages);
            Options = options;
            return Task.FromResult(response);
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
