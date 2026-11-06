using Datapoint.Terminal.Agent.Providers;

namespace Datapoint.Terminal.Settings;

/// <summary>Models we suggest for each provider. The user can type any other model name.</summary>
public static class ProviderCatalog
{
    public static IReadOnlyList<string> Models(ProviderKind kind) => kind switch
    {
        ProviderKind.Anthropic => ["claude-opus-5-5", "claude-sonnet-5-5", "claude-haiku-5-5"],
        _ => [],
    };

    public static string DefaultName(ProviderKind kind) => kind switch
    {
        ProviderKind.Anthropic => "Anthropic",
        ProviderKind.OpenAI => "OpenAI",
        _ => "OpenAI-compatible server",
    };

    public static string DefaultModel(ProviderKind kind) => kind == ProviderKind.Anthropic ? ProviderSettings.DefaultAnthropicModel : string.Empty;

    public static bool NeedsKey(ProviderKind kind) => kind != ProviderKind.OpenAICompatible;
}
