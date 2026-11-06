using Datapoint.Terminal.Agent.Providers;
using Datapoint.Terminal.Security.Credentials;

namespace Datapoint.Terminal.Settings;

/// <summary>
/// API keys for the user's providers. They live in the operating system's credential store; when there is none (e.g. Linux
/// without a keyring), only the provider's usual environment variable can supply a key, and the application says so.
/// </summary>
public sealed class ProviderKeys(ICredentialStore? store, Func<string, string?> environment)
{
    public const string Service = "Datapoint Terminal";

    public ICredentialStore? Store { get; } = store;

    public bool CanSave => Store is not null;

    /// <summary>Where keys are kept, for the user: "Keychain", "Credential Manager", ...</summary>
    public string? StoreName => Store?.Name;

    public string? Read(ProviderAccount provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return Store?.Read(Service, Account(provider)) ?? FromEnvironment(provider.Kind);
    }

    /// <summary>Whether the key comes from an environment variable rather than the store, and which.</summary>
    public string? EnvironmentVariableFor(ProviderAccount provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (Store?.Read(Service, Account(provider)) is not null)
        {
            return null;
        }

        var name = VariableName(provider.Kind);
        return name is not null && !string.IsNullOrEmpty(environment(name)) ? name : null;
    }

    public bool HasSavedKey(ProviderAccount provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return Store?.Read(Service, Account(provider)) is not null;
    }

    public void Save(ProviderAccount provider, string key)
    {
        ArgumentNullException.ThrowIfNull(provider);
        (Store ?? throw new CredentialStoreException("This system has no credential store to keep the key in.")).Save(Service, Account(provider), key.Trim());
    }

    public void Delete(ProviderAccount provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        Store?.Delete(Service, Account(provider));
    }

    private static string Account(ProviderAccount provider) => "provider/" + provider.Id;

    private static string? VariableName(ProviderKind kind) => kind switch
    {
        ProviderKind.Anthropic => "ANTHROPIC_API_KEY",
        ProviderKind.OpenAI => "OPENAI_API_KEY",
        _ => null,
    };

    private string? FromEnvironment(ProviderKind kind) => VariableName(kind) is { } name && environment(name) is { Length: > 0 } key ? key : null;
}
