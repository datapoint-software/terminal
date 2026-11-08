using Datapoint.Terminal.Agent.Providers;
using Datapoint.Terminal.Agent.Web;
using Datapoint.Terminal.Settings;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Shell;

public sealed partial class MainWindow
{
    private IChatClient? _client;
    private IWebSearch? _webSearch;
    private string? _webSearchSignature;

    // The agent's web permission and search, and the commands it's been taught are read-only, follow the settings, which the user can change from any window.
    private void ApplyWebSettings()
    {
        _chat.IsModelLocal = _provider?.IsLocal == true;
        if (_agent is not { } agent)
        {
            return;
        }

        agent.IsWebAlwaysAllowed = _settings.AlwaysAllowWeb;
        agent.LearnedCommands = _settings.LearnedCommands;
        var service = _settings.WebSearch;
        var key = service is WebSearchService.Brave or WebSearchService.Tavily ? ReadSearchKey(service) : null;
        var signature = string.Join('|', service, _settings.SearxngServer, key?.GetHashCode(StringComparison.Ordinal), _provider?.Id, _client?.GetHashCode());
        if (string.Equals(signature, _webSearchSignature, StringComparison.Ordinal))
        {
            return;
        }

        _webSearchSignature = signature;
        (_webSearch as IDisposable)?.Dispose();
        _webSearch = CreateWebSearch(service, key);
        agent.WebSearch = _webSearch;
    }

    private IWebSearch? CreateWebSearch(WebSearchService service, string? key) => service switch
    {
        WebSearchService.Brave when key is not null => new BraveWebSearch(key),
        WebSearchService.Tavily when key is not null => new TavilyWebSearch(key),
        WebSearchService.Searxng when SearxngServer() is { } server => new SearxngWebSearch(server),
        WebSearchService.Provider when _client is not null && _provider is { Kind: ProviderKind.Anthropic or ProviderKind.OpenAI } provider =>
            new ProviderWebSearch(_client, ProviderPrivacy.Destination(provider)),
        _ => null,
    };

    private Uri? SearxngServer() =>
        Uri.TryCreate(_settings.SearxngServer, UriKind.Absolute, out var server) && WebAddressGuard.IsWebScheme(server.Scheme) ? server : null;

    private string? ReadSearchKey(WebSearchService service)
    {
        try
        {
            var key = _settings.WebKeys.Read(service);
            _redactor.AddKnownSecret(key ?? string.Empty);
            return key;
        }
        catch (Security.Credentials.CredentialStoreException)
        {
            return null;
        }
    }
}
