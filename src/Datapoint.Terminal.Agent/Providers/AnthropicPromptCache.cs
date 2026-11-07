using Anthropic.Models.Messages;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Agent.Providers;

// Anthropic caches only what a request marks, unlike OpenAI, which caches a repeated prefix by itself. Every step of the
// agent's loop resends the tools, the system prompt and the conversation so far, so this marks two cache points: the end of
// the system prompt, which stays the same for a whole conversation, and the end of the newest message, so the next step
// reads everything before it from the cache. Marks from the previous request are cleared first: the history's contents are
// shared between requests, and Anthropic allows at most four marks.
public sealed class AnthropicPromptCache(IChatClient inner) : DelegatingChatClient(inner)
{
    public override Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var list = Mark(messages);
        return base.GetResponseAsync(list, options, cancellationToken);
    }

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var list = Mark(messages);
        return base.GetStreamingResponseAsync(list, options, cancellationToken);
    }

    private static List<ChatMessage> Mark(IEnumerable<ChatMessage> messages)
    {
        var list = messages as List<ChatMessage> ?? [.. messages];
        // Only a block with extra properties can hold a mark.
        foreach (var content in list.SelectMany(message => message.Contents).Where(content => content.AdditionalProperties is { Count: > 0 }))
        {
            content.WithCacheControl((CacheControlEphemeral?)null);
        }

        MarkLast(list.LastOrDefault(message => message.Role == ChatRole.System));
        MarkLast(list.LastOrDefault(message => message.Role != ChatRole.System));
        return list;
    }

    // Reasoning can't carry a cache mark, so the mark goes on the last block that can.
    private static void MarkLast(ChatMessage? message)
    {
        if (message?.Contents.LastOrDefault(content => content is not TextReasoningContent) is not { } content)
        {
            return;
        }

        content.WithCacheControl(new CacheControlEphemeral());
    }
}
