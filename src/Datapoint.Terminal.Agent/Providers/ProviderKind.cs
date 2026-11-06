namespace Datapoint.Terminal.Agent.Providers;

public enum ProviderKind
{
    Anthropic,
    OpenAI,

    /// <summary>An open-weight model behind an OpenAI-compatible API (Ollama, LM Studio, vLLM or a hosting service).</summary>
    OpenAICompatible,
}
