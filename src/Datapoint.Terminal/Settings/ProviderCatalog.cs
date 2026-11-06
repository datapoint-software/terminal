using System.Text.RegularExpressions;
using Datapoint.Terminal.Agent.Providers;

namespace Datapoint.Terminal.Settings;

/// <summary>Models we suggest for each provider. The user can type any other model name.</summary>
public static partial class ProviderCatalog
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

    /// <summary>A short name for a model, as Anthropic writes it (claude-opus-5-5 is "Opus 5.5"); other models keep their ID.</summary>
    public static string DisplayName(string model)
    {
        var match = ClaudeModel().Match(model);
        if (!match.Success)
        {
            return model;
        }

        var family = match.Groups["family"].Value;
        var version = match.Groups["minor"].Success ? $"{match.Groups["major"].Value}.{match.Groups["minor"].Value}" : match.Groups["major"].Value;
        return $"{char.ToUpperInvariant(family[0])}{family[1..]} {version}";
    }

    [GeneratedRegex(@"^claude-(?<family>[a-z]+)-(?<major>\d+)(?:-(?<minor>\d{1,2}))?(?:-\d{8})?$", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex ClaudeModel();
}
