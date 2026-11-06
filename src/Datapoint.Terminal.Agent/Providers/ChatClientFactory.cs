using System.ClientModel;
using Anthropic;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Datapoint.Terminal.Agent.Providers;

/// <summary>
/// Creates the <see cref="IChatClient"/> for a provider: Anthropic's own SDK (the only one that understands its prompt caching),
/// OpenAI's Responses API for OpenAI, and Chat Completions for OpenAI-compatible servers, whose support for Responses varies.
/// </summary>
public static class ChatClientFactory
{
    public static IChatClient Create(ProviderSettings settings, string? apiKey)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Kind switch
        {
            ProviderKind.Anthropic => new AnthropicClient { ApiKey = Require(apiKey, "Anthropic") }.AsIChatClient(settings.Model),
            ProviderKind.OpenAI => CreateOpenAI(settings, apiKey),
            _ => CreateCompatible(settings, apiKey),
        };
    }

    // The Responses adapter is marked experimental (OPENAI001); engineering.md chooses the Responses API for OpenAI itself.
#pragma warning disable OPENAI001
    private static IChatClient CreateOpenAI(ProviderSettings settings, string? apiKey) =>
        new OpenAIClient(new ApiKeyCredential(Require(apiKey, "OpenAI"))).GetResponsesClient().AsIChatClient(settings.Model);
#pragma warning restore OPENAI001

    private static IChatClient CreateCompatible(ProviderSettings settings, string? apiKey)
    {
        var endpoint = settings.Endpoint ?? throw new ArgumentException("An OpenAI-compatible provider needs the server's address.", nameof(settings));
        var options = new OpenAIClientOptions { Endpoint = endpoint };

        // Local servers usually take no key, but the client requires one.
        var credential = new ApiKeyCredential(string.IsNullOrEmpty(apiKey) ? "none" : apiKey);
        return new OpenAIClient(credential, options).GetChatClient(settings.Model).AsIChatClient();
    }

    private static string Require(string? apiKey, string provider) =>
        string.IsNullOrEmpty(apiKey) ? throw new ArgumentException($"An {provider} API key is needed.", nameof(apiKey)) : apiKey;
}
