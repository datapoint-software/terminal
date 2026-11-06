using Datapoint.Terminal.Agent.Providers;

namespace Datapoint.Terminal.Settings;

/// <summary>A provider the user added. Its API key isn't here: it's in the credential store, under <see cref="Id"/>.</summary>
public sealed record ProviderAccount
{
    public required string Id { get; init; }

    public ProviderKind Kind { get; init; }

    /// <summary>The name the user sees, e.g. "Anthropic" or "Ollama on my desktop".</summary>
    public required string Name { get; init; }

    /// <summary>The model the agent uses with this provider.</summary>
    public required string Model { get; init; }

    /// <summary>The server's address, for OpenAI-compatible servers.</summary>
    public string? Endpoint { get; init; }

    /// <summary>The user says the server runs on their own machine or network, so nothing leaves it.</summary>
    public bool IsLocal { get; init; }

    public static string NewId() => Guid.NewGuid().ToString("N");

    public ProviderSettings ToProviderSettings() => new()
    {
        Kind = Kind,
        Model = Model,
        Endpoint = Uri.TryCreate(Endpoint, UriKind.Absolute, out var endpoint) ? endpoint : null,
        IsLocal = IsLocal,
    };
}
