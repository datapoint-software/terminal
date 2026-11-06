namespace Datapoint.Terminal.Agent.Providers;

/// <summary>Which provider and model the agent uses. The API key isn't part of it: it comes from the credential store.</summary>
public sealed record ProviderSettings
{
    public const string DefaultAnthropicModel = "claude-opus-5-5";

    public ProviderKind Kind { get; init; } = ProviderKind.Anthropic;

    public string Model { get; init; } = DefaultAnthropicModel;

    /// <summary>The server's address, for OpenAI-compatible servers (e.g. http://localhost:11434/v1 for Ollama).</summary>
    public Uri? Endpoint { get; init; }

    /// <summary>Set by the user for a model on their own machine or network, so the application can say nothing leaves it.</summary>
    public bool IsLocal { get; init; }
}
