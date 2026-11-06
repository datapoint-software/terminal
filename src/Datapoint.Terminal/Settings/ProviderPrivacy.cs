using Datapoint.Terminal.Agent.Providers;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Settings;

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
