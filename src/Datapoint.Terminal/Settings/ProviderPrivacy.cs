using Datapoint.Terminal.Agent.Providers;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Settings;

/// <summary>Where the conversation goes with a provider, in the user's words.</summary>
public static class ProviderPrivacy
{
    public static string Describe(ProviderAccount provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return provider.Kind switch
        {
            ProviderKind.Anthropic => Strings.Format(Strings.ProviderSendsTo, "Anthropic (api.anthropic.com)"),
            ProviderKind.OpenAI => Strings.Format(Strings.ProviderSendsTo, "OpenAI (api.openai.com)"),
            _ when Uri.TryCreate(provider.Endpoint, UriKind.Absolute, out var uri) => provider.IsLocal
                ? Strings.Format(Strings.ProviderStaysLocal, uri.Authority)
                : Strings.Format(Strings.ProviderSendsTo, uri.Authority),
            _ => string.Empty,
        };
    }

    /// <summary>A short name for where the conversation goes, e.g. "Anthropic" or "localhost:11434".</summary>
    public static string Destination(ProviderAccount provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return provider.Kind switch
        {
            ProviderKind.Anthropic => "Anthropic",
            ProviderKind.OpenAI => "OpenAI",
            _ => Uri.TryCreate(provider.Endpoint, UriKind.Absolute, out var uri) ? uri.Authority : provider.Name,
        };
    }
}
