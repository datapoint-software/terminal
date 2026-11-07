using Datapoint.Terminal.Security.Credentials;

namespace Datapoint.Terminal.Settings;

// Search keys live in the system's credential store beside the provider keys, or come from the environment where there's none.
public sealed class WebSearchKeys(ICredentialStore? store, Func<string, string?> environment)
{
    public bool CanSave => store is not null;

    public string? StoreName => store?.Name;

    public string? Read(WebSearchService service) => store?.Read(ProviderKeys.Service, Account(service)) ?? FromEnvironment(service);

    public bool HasSavedKey(WebSearchService service) => store?.Read(ProviderKeys.Service, Account(service)) is not null;

    // The variable a key would come from, when it does.
    public string? EnvironmentVariableFor(WebSearchService service) =>
        !HasSavedKey(service) && VariableName(service) is { } name && !string.IsNullOrEmpty(environment(name)) ? name : null;

    public void Save(WebSearchService service, string key) =>
        (store ?? throw new CredentialStoreException("This system has no credential store to keep the key in.")).Save(ProviderKeys.Service, Account(service), key.Trim());

    public void Delete(WebSearchService service) => store?.Delete(ProviderKeys.Service, Account(service));

    public static string? VariableName(WebSearchService service) => service switch
    {
        WebSearchService.Brave => "BRAVE_API_KEY",
        WebSearchService.Tavily => "TAVILY_API_KEY",
        _ => null,
    };

    private static string Account(WebSearchService service) => "web-search/" + service.ToString().ToLowerInvariant();

    private string? FromEnvironment(WebSearchService service) => VariableName(service) is { } name && environment(name) is { Length: > 0 } key ? key : null;
}
