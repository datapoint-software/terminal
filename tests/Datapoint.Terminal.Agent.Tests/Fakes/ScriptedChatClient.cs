using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Agent.Tests.Fakes;

/// <summary>A model that answers from a script: each call takes the next step, given the conversation so far.</summary>
internal sealed class ScriptedChatClient(params Func<IReadOnlyList<ChatMessage>, ChatResponse>[] steps) : IChatClient
{
    private int _next;

    public List<List<ChatMessage>> Requests { get; } = [];

    public static ChatResponse Text(string text) => new(new ChatMessage(ChatRole.Assistant, text));

    public static ChatResponse Call(string name, IDictionary<string, object?> arguments, string id = "call-1") =>
        new(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent(id, name, arguments)]));

    public static string LastToolResult(IReadOnlyList<ChatMessage> messages) =>
        messages.Last(message => message.Role == ChatRole.Tool).Contents.OfType<FunctionResultContent>().Last().Result?.ToString() ?? string.Empty;

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(Next([.. messages]));

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = Next([.. messages]);
        foreach (var update in response.ToChatResponseUpdates())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }

    private ChatResponse Next(List<ChatMessage> messages)
    {
        Requests.Add(messages);
        var step = _next < steps.Length ? steps[_next] : _ => Text("(script ended)");
        _next++;
        return step(messages);
    }
}
