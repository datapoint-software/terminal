using System.Text.Json.Serialization;
using Datapoint.Terminal.Agent.Providers;

namespace Datapoint.Terminal.Settings;

// The API key isn't here: it's in the credential store, under Id.
public sealed record ProviderAccount
{
    public required string Id { get; init; }

    public ProviderKind Kind { get; init; }

    public required string Name { get; init; }

    public required string Model { get; init; }

    public string? Endpoint { get; init; }

    // The user's own word that the server runs on their machine or network, so nothing leaves it.
    public bool IsLocal { get; init; }

    // In tokens, as the user gave it; unknown otherwise, since providers don't all report it.
    public int? ContextWindow { get; init; }

    // In characters; null uses the limit worked out from the context window.
    public int? OutputLimit { get; init; }

    // In US dollars per million tokens, as the user's provider charges; optional, for the cost estimate.
    public decimal? InputPrice { get; init; }

    public decimal? OutputPrice { get; init; }

    // For input read from the prompt cache; optional.
    public decimal? CachedInputPrice { get; init; }

    // A model the app knows has its provider's own context window and prices, whatever was saved for it (product decision 126).
    [JsonIgnore]
    public KnownModel? Known => ProviderCatalog.Find(Kind, Model);

    [JsonIgnore]
    public int? EffectiveContextWindow => Known?.ContextWindow ?? ContextWindow;

    [JsonIgnore]
    public Agent.Usage.TokenPrice? Price => Known?.Price ?? ToPrice();

    // Without a cached price, Anthropic's published rates apply to it (a tenth of the input price to read the cache, 1.25 times
    // to write it for five minutes); other providers' cached input counts at the full input price, which may overstate the
    // cost but never understates it against a spending limit.
    private Agent.Usage.TokenPrice? ToPrice() => InputPrice is { } input && OutputPrice is { } output
        ? new(input, output)
        {
            CachedInputPerMillion = CachedInputPrice ?? (Kind == ProviderKind.Anthropic ? input * 0.1m : null),
            CacheWritePerMillion = Kind == ProviderKind.Anthropic ? input * 1.25m : null,
        }
        : null;

    public static string NewId() => Guid.NewGuid().ToString("N");

    public ProviderSettings ToProviderSettings() => new()
    {
        Kind = Kind,
        Model = Model,
        Endpoint = Uri.TryCreate(Endpoint, UriKind.Absolute, out var endpoint) ? endpoint : null,
        IsLocal = IsLocal,
    };
}
