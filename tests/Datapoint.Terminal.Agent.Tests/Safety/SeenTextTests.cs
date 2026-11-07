using Datapoint.Terminal.Agent.Safety;

namespace Datapoint.Terminal.Agent.Tests.Safety;

public sealed class SeenTextTests
{
    private static SeenText Seen(string text)
    {
        var seen = new SeenText();
        seen.Add(text);
        return seen;
    }

    [Fact]
    public void Finds_terminal_text_in_an_address_even_encoded_or_reformatted()
    {
        var seen = Seen("DATABASE_HOST=orders-primary.eu-west-1.rds.example.com\nport 5432");

        Assert.Contains("orders-primary.eu-west-1.rds.example.com", seen.FindIn("https://x.example/?q=Orders-Primary.eu-west-1.rds.example.com"), StringComparison.Ordinal);
        Assert.NotNull(seen.FindIn("https://x.example/?q=database_host%3Dorders-primary.eu-west-1"));
    }

    [Fact]
    public void Ignores_short_overlaps_and_unrelated_text()
    {
        var seen = Seen("nginx: [emerg] unknown directive \"proxy_passs\"");

        Assert.Null(seen.FindIn("https://nginx.org/en/docs/"));
        Assert.Null(seen.FindIn("https://example.org/a-completely-different-long-path-here"));
    }

    [Fact]
    public void Knows_text_printed_whole()
    {
        var seen = Seen("See https://nginx.org/en/docs/http/ngx_http_proxy_module.html for details");

        Assert.True(seen.Contains("https://nginx.org/en/docs/http/ngx_http_proxy_module.html"));
        Assert.False(seen.Contains("https://nginx.org/en/docs/other.html"));
    }

    [Fact]
    public void Forgets_everything_when_cleared()
    {
        var seen = Seen("a-long-enough-value-from-the-terminal-output");

        seen.Clear();

        Assert.Null(seen.FindIn("https://x.example/a-long-enough-value-from-the-terminal-output"));
    }
}
