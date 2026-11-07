using System.Net;
using System.Text;
using Datapoint.Terminal.Agent.Web;

namespace Datapoint.Terminal.Agent.Tests.Web;

public sealed class WebPageReaderTests
{
    private static readonly Uri s_page = new("https://docs.example.org/guide/");

    [Fact]
    public async Task Reads_a_page_as_markdown_with_its_title()
    {
        using var reader = Reader(_ => Html("<html><head><title>Guide</title></head><body><main><h1>Install</h1><p>Run it.</p></main></body></html>"));

        var page = await reader.ReadAsync(s_page, CancellationToken.None);

        Assert.Null(page.Problem);
        Assert.Equal("Guide", page.Title);
        Assert.Equal("# Install\n\nRun it.", page.Text);
    }

    [Fact]
    public async Task Follows_a_redirect_to_a_public_address()
    {
        var target = new Uri("https://docs.example.org/guide/v2/");
        using var reader = Reader(request => request.RequestUri == target ? Html("<p>Moved here</p>") : Redirect("/guide/v2/"));

        var page = await reader.ReadAsync(s_page, CancellationToken.None);

        Assert.Equal(target, page.Address);
        Assert.Equal("Moved here", page.Text);
    }

    [Theory]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://localhost/admin")]
    [InlineData("file:///etc/passwd")]
    public async Task Refuses_a_redirect_to_a_private_address(string location)
    {
        var requests = 0;
        using var reader = Reader(_ =>
        {
            requests++;
            return Redirect(location);
        });

        var page = await reader.ReadAsync(s_page, CancellationToken.None);

        Assert.NotNull(page.Problem);
        Assert.Equal(1, requests);
    }

    [Fact]
    public async Task Stops_after_too_many_redirects()
    {
        var hops = 0;
        using var reader = Reader(_ => Redirect($"/hop/{++hops}"));

        var page = await reader.ReadAsync(s_page, CancellationToken.None);

        Assert.NotNull(page.Problem);
        Assert.True(hops <= 6);
    }

    [Fact]
    public async Task Refuses_content_that_is_not_text()
    {
        using var reader = Reader(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) { Headers = { ContentType = new("application/octet-stream") } } });

        var page = await reader.ReadAsync(s_page, CancellationToken.None);

        Assert.Contains("application/octet-stream", page.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reports_an_error_status()
    {
        using var reader = Reader(_ => new HttpResponseMessage(HttpStatusCode.NotFound) { ReasonPhrase = "Not Found" });

        var page = await reader.ReadAsync(s_page, CancellationToken.None);

        Assert.Equal("The server answered 404 Not Found.", page.Problem);
    }

    [Fact]
    public async Task Cuts_long_pages()
    {
        using var reader = Reader(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new string('a', WebPageReader.MaxCharacters + 10), Encoding.UTF8, "text/plain") });

        var page = await reader.ReadAsync(s_page, CancellationToken.None);

        Assert.True(page.IsCut);
        Assert.Equal(WebPageReader.MaxCharacters, page.Text.Length);
    }

    // A public name can be pointed at a private address; the check happens on the address actually connected to.
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("10.1.2.3")]
    public async Task Refuses_a_public_name_that_resolves_to_a_private_address(string resolved)
    {
        using var reader = new WebPageReader((_, _) => Task.FromResult(new[] { IPAddress.Parse("93.184.216.34"), IPAddress.Parse(resolved) }));

        var page = await reader.ReadAsync(s_page, CancellationToken.None);

        Assert.Contains("private network", page.Problem, StringComparison.Ordinal);
    }

    private static WebPageReader Reader(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new((_, _) => Task.FromResult(Array.Empty<IPAddress>()), new FakeHandler(respond));

    private static HttpResponseMessage Html(string html) => new(HttpStatusCode.OK) { Content = new StringContent(html, Encoding.UTF8, "text/html") };

    private static HttpResponseMessage Redirect(string location) => new(HttpStatusCode.Found) { Headers = { Location = new Uri(location, UriKind.RelativeOrAbsolute) } };

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }
}
