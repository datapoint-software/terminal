using Datapoint.Terminal.Agent.Providers;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Chat;

/// <summary>
/// Reads the provider from environment variables until settings and the credential store exist (docs/autopilot.md):
/// DATAPOINT_PROVIDER (anthropic, openai or compatible), DATAPOINT_MODEL, DATAPOINT_ENDPOINT, and the provider's usual key
/// variable, ANTHROPIC_API_KEY or OPENAI_API_KEY.
/// </summary>
public static class ProviderFromEnvironment
{
    public sealed record Choice(ProviderSettings Settings, string? ApiKey, string PrivacyNotice);

    /// <summary>The provider to use, or null when none is set up.</summary>
    public static Choice? Read(Func<string, string?> variable)
    {
        ArgumentNullException.ThrowIfNull(variable);
        var kind = variable("DATAPOINT_PROVIDER")?.Trim().ToUpperInvariant() switch
        {
            "OPENAI" => ProviderKind.OpenAI,
            "COMPATIBLE" or "OPENAI-COMPATIBLE" => ProviderKind.OpenAICompatible,
            _ => ProviderKind.Anthropic,
        };
        var model = variable("DATAPOINT_MODEL")?.Trim();
        var endpoint = Uri.TryCreate(variable("DATAPOINT_ENDPOINT"), UriKind.Absolute, out var uri) ? uri : null;
        return kind switch
        {
            ProviderKind.Anthropic => Anthropic(variable("ANTHROPIC_API_KEY"), model),
            ProviderKind.OpenAI => OpenAI(variable("OPENAI_API_KEY"), model),
            _ => Compatible(variable("OPENAI_API_KEY"), model, endpoint),
        };
    }

    private static Choice? Anthropic(string? key, string? model) =>
        string.IsNullOrWhiteSpace(key)
            ? null
            : new Choice(
                new ProviderSettings { Kind = ProviderKind.Anthropic, Model = string.IsNullOrEmpty(model) ? ProviderSettings.DefaultAnthropicModel : model },
                key,
                Strings.Format(Strings.NoticeSentTo, "Anthropic", "api.anthropic.com"));

    private static Choice? OpenAI(string? key, string? model) =>
        string.IsNullOrWhiteSpace(key) || string.IsNullOrEmpty(model)
            ? null
            : new Choice(new ProviderSettings { Kind = ProviderKind.OpenAI, Model = model }, key, Strings.Format(Strings.NoticeSentTo, "OpenAI", "api.openai.com"));

    private static Choice? Compatible(string? key, string? model, Uri? endpoint)
    {
        if (endpoint is null || string.IsNullOrEmpty(model))
        {
            return null;
        }

        var isLocal = endpoint.IsLoopback;
        var notice = isLocal ? Strings.Format(Strings.NoticeLocal, endpoint.Authority) : Strings.Format(Strings.NoticeSentTo, endpoint.Host, endpoint.Authority);
        return new Choice(new ProviderSettings { Kind = ProviderKind.OpenAICompatible, Model = model, Endpoint = endpoint, IsLocal = isLocal }, key, notice);
    }
}
