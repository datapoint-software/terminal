using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Tests.Chat;

/// <summary>A model that replies from a list, repeating the last reply.</summary>
internal sealed class ListChatClient(params ChatResponse[] responses) : IChatClient
{
    private int _next;

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(responses[Math.Min(_next++, responses.Length - 1)]);

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var update in (await GetResponseAsync(messages, options, cancellationToken)).ToChatResponseUpdates())
        {
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
