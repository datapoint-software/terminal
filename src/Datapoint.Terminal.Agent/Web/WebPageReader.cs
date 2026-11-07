using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;

namespace Datapoint.Terminal.Agent.Web;

// Reads one public web page as text for the agent. Every address, including each redirect, passes WebAddressGuard, and the
// connection is made to an address checked after looking the host up, so a name that resolves to a private address the
// second time can't get through. No cookies, credentials or proxy: a proxy would look the host up itself, past the check.
public sealed class WebPageReader : IDisposable
{
    private const int MaxBytes = 3 * 1024 * 1024;
    private const int MaxRedirects = 5;
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(20);

    private readonly HttpClient _client;

    public WebPageReader()
        : this(LookUpAsync)
    {
    }

    // Tests give their own lookup, so they run without the network.
    internal WebPageReader(Func<string, CancellationToken, Task<IPAddress[]>> lookUp, HttpMessageHandler? handler = null)
    {
        _client = new HttpClient(handler ?? CreateHandler(lookUp)) { Timeout = s_timeout };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; DatapointTerminal)");
        _client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,text/plain;q=0.9,text/markdown;q=0.9,*/*;q=0.5");
        _client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en;q=0.9,*;q=0.5");
    }

    public void Dispose() => _client.Dispose();

    public async Task<WebPage> ReadAsync(Uri address, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);
        for (var redirects = 0; ; redirects++)
        {
            if (WebAddressGuard.Check(address) is { } refused)
            {
                return WebPage.Failed(address, refused);
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, address);
            HttpResponseMessage response;
            try
            {
                response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException exception)
            {
                return WebPage.Failed(address, exception.InnerException is BlockedAddressException blocked ? blocked.Message : $"The page couldn't be reached: {exception.Message}");
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return WebPage.Failed(address, "The page took too long to answer.");
            }

            using (response)
            {
                if (IsRedirect(response.StatusCode) && response.Headers.Location is { } location)
                {
                    if (redirects >= MaxRedirects)
                    {
                        return WebPage.Failed(address, "The page redirected too many times.");
                    }

                    address = location.IsAbsoluteUri ? location : new Uri(address, location);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    return WebPage.Failed(address, $"The server answered {(int)response.StatusCode} {response.ReasonPhrase}.");
                }

                return await ReadBodyAsync(address, response.Content, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task<WebPage> ReadBodyAsync(Uri address, HttpContent content, CancellationToken cancellationToken)
    {
        var type = content.Headers.ContentType?.MediaType ?? "text/html";
        var isHtml = type is "text/html" or "application/xhtml+xml";
        if (!isHtml && !type.StartsWith("text/", StringComparison.Ordinal) && type is not ("application/json" or "application/xml"))
        {
            return WebPage.Failed(address, $"The page is {type}, not text the agent can read.");
        }

        var bytes = await ReadLimitedAsync(content, cancellationToken).ConfigureAwait(false);
        var text = Decode(bytes, content.Headers.ContentType);
        if (!isHtml)
        {
            return new WebPage(address, Title: null, text, Problem: null);
        }

        var html = HtmlText.ToMarkdown(text, address);
        return new WebPage(address, html.Title, html.Text, Problem: null) { Links = html.Links };
    }

    private static async Task<byte[]> ReadLimitedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while (buffer.Length < MaxBytes && (read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await buffer.WriteAsync(chunk.AsMemory(0, (int)Math.Min(read, MaxBytes - buffer.Length)), cancellationToken).ConfigureAwait(false);
            }

            return buffer.ToArray();
        }
    }

    private static string Decode(byte[] bytes, MediaTypeHeaderValue? type)
    {
        try
        {
            if (type?.CharSet is { Length: > 0 } charset)
            {
                return Encoding.GetEncoding(charset.Trim('"')).GetString(bytes);
            }
        }
        catch (ArgumentException)
        {
            // An unknown character set falls back to UTF-8, which most pages use.
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private static bool IsRedirect(HttpStatusCode status) => (int)status is 301 or 302 or 303 or 307 or 308;

    private static SocketsHttpHandler CreateHandler(Func<string, CancellationToken, Task<IPAddress[]>> lookUp) => new()
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        UseProxy = false,
        Credentials = null,
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        ConnectCallback = async (context, cancellationToken) =>
        {
            var host = context.DnsEndPoint.Host;
            var addresses = IPAddress.TryParse(host, out var literal) ? [literal] : await lookUp(host, cancellationToken).ConfigureAwait(false);
            if (addresses.Length == 0)
            {
                throw new BlockedAddressException($"{host} couldn't be found.");
            }

            // Every address the name has must be public, or a server could choose to answer from the private one.
            if (addresses.FirstOrDefault(address => !WebAddressGuard.IsPublic(address)) is not null)
            {
                throw new BlockedAddressException($"{host} is on this machine or a private network, so it can't be read.");
            }

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken).ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        },
    };

    private static Task<IPAddress[]> LookUpAsync(string host, CancellationToken cancellationToken) => Dns.GetHostAddressesAsync(host, cancellationToken);

    private sealed class BlockedAddressException(string message) : IOException(message);
}
