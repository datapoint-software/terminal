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

    public Agent.Usage.TokenPrice? ToPrice() => InputPrice is { } input && OutputPrice is { } output ? new(input, output) : null;

    public static string NewId() => Guid.NewGuid().ToString("N");

    public ProviderSettings ToProviderSettings() => new()
    {
        Kind = Kind,
        Model = Model,
        Endpoint = Uri.TryCreate(Endpoint, UriKind.Absolute, out var endpoint) ? endpoint : null,
        IsLocal = IsLocal,
    };
}
