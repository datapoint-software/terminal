using System.Text.RegularExpressions;
using Datapoint.Terminal.Agent.Providers;
using Datapoint.Terminal.Agent.Usage;

namespace Datapoint.Terminal.Settings;

public static partial class ProviderCatalog
{
    // Product decision 126: the current models of each provider, with their context window and prices, as the providers
    // publish them. Checked on 2026-11-08 against platform.claude.com/docs/en/about-claude/pricing and models/overview, and
    // developers.openai.com/api/docs/pricing and models; update them, and PricesAsOf, together.
    public static readonly DateOnly PricesAsOf = new(2026, 11, 8);

    public static IReadOnlyList<KnownModel> Known { get; } =
    [
        new(ProviderKind.Anthropic, "claude-fable-5-1", "Claude Fable 5.1", 1_000_000, Price(10m, 50m, 0.25m, 12.5m)),
        new(ProviderKind.Anthropic, "claude-opus-5-5", "Claude Opus 5.5", 1_000_000, Price(4m, 20m, 0.2m, 5m)),
        new(ProviderKind.Anthropic, "claude-sonnet-5-5", "Claude Sonnet 5.5", 1_000_000, Price(2m, 10m, 0.1m, 2.5m)),
        new(ProviderKind.Anthropic, "claude-haiku-5-5", "Claude Haiku 5.5", 1_000_000, Price(0.1m, 0.5m, 0.01m, 0.125m) with { LongPromptAbove = 100_000, LongPrompt = Price(0.5m, 2.5m, 0.05m, 0.625m) }),
        new(ProviderKind.OpenAI, "gpt-6-astra", "GPT-6 Astra", 1_050_000, Price(10m, 50m, 1m) with { LongPromptAbove = 272_000, LongPrompt = Price(20m, 75m, 2m) }),
        new(ProviderKind.OpenAI, "gpt-6.1-sol", "GPT-6.1 Sol", 1_050_000, Price(2m, 10m, 0.1m) with { LongPromptAbove = 272_000, LongPrompt = Price(4m, 15m, 0.2m) }),
        new(ProviderKind.OpenAI, "gpt-6.1-terra", "GPT-6.1 Terra", 1_050_000, Price(1m, 5m, 0.05m) with { LongPromptAbove = 272_000, LongPrompt = Price(2m, 7.5m, 0.1m) }),
        new(ProviderKind.OpenAI, "gpt-6-luna", "GPT-6 Luna", 1_050_000, Price(0.1m, 0.5m, 0.01m) with { LongPromptAbove = 272_000, LongPrompt = Price(0.2m, 0.75m, 0.02m) }),
    ];

    public static IReadOnlyList<string> Models(ProviderKind kind) => [.. Known.Where(model => model.Kind == kind).Select(model => model.Id)];

    // A compatible server's models aren't ours to know, whatever they're called.
    public static KnownModel? Find(ProviderKind kind, string? model) =>
        kind == ProviderKind.OpenAICompatible ? null : Known.FirstOrDefault(item => item.Kind == kind && string.Equals(item.Id, model?.Trim(), StringComparison.Ordinal));

    // Writing to OpenAI's cache costs nothing extra, and it doesn't report it.
    private static TokenPrice Price(decimal input, decimal output, decimal cacheRead, decimal? cacheWrite = null) =>
        new(input, output) { CachedInputPerMillion = cacheRead, CacheWritePerMillion = cacheWrite };

    public static string DefaultName(ProviderKind kind) => kind switch
    {
        ProviderKind.Anthropic => "Anthropic",
        ProviderKind.OpenAI => "OpenAI",
        _ => "OpenAI-compatible server",
    };

    // OpenAI suggests GPT-6 Astra to start with.
    public static string DefaultModel(ProviderKind kind) => kind switch
    {
        ProviderKind.Anthropic => ProviderSettings.DefaultAnthropicModel,
        ProviderKind.OpenAI => "gpt-6-astra",
        _ => string.Empty,
    };

    public static bool NeedsKey(ProviderKind kind) => kind != ProviderKind.OpenAICompatible;

    public static string DisplayName(string model)
    {
        if (Known.FirstOrDefault(item => string.Equals(item.Id, model, StringComparison.Ordinal)) is { } known)
        {
            return known.Name;
        }

        var match = ClaudeModel().Match(model);
        if (!match.Success)
        {
            return model;
        }

        var family = match.Groups["family"].Value;
        var version = match.Groups["minor"].Success ? $"{match.Groups["major"].Value}.{match.Groups["minor"].Value}" : match.Groups["major"].Value;
        return $"Claude {char.ToUpperInvariant(family[0])}{family[1..]} {version}";
    }

    [GeneratedRegex(@"^claude-(?<family>[a-z]+)-(?<major>\d+)(?:-(?<minor>\d{1,2}))?(?:-\d{8})?$", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex ClaudeModel();
}
