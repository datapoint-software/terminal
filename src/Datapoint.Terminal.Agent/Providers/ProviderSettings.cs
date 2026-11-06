namespace Datapoint.Terminal.Agent.Providers;

// The API key isn't part of it: it comes from the credential store.
public sealed record ProviderSettings
{
    public const string DefaultAnthropicModel = "claude-opus-5-5";

    public ProviderKind Kind { get; init; } = ProviderKind.Anthropic;

    public string Model { get; init; } = DefaultAnthropicModel;

    public Uri? Endpoint { get; init; }

    // Set by the user for a model on their own machine or network, so the application can say nothing leaves it.
    public bool IsLocal { get; init; }
}
