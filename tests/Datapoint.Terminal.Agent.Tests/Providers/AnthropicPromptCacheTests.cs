using System.Net;
using System.Text;
using System.Text.Json;
using Anthropic;
using Datapoint.Terminal.Agent.Providers;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Agent.Tests.Providers;

public sealed class AnthropicPromptCacheTests
{
    private const string Stream = """
        event: message_start
        data: {"type":"message_start","message":{"id":"msg_1","type":"message","role":"assistant","model":"claude-sonnet-5-5","content":[],"stop_reason":null,"stop_sequence":null,"usage":{"input_tokens":10,"cache_read_input_tokens":2000,"cache_creation_input_tokens":300,"output_tokens":1}}}

        event: content_block_start
        data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

        event: content_block_delta
        data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Hi"}}

        event: content_block_stop
        data: {"type":"content_block_stop","index":0}

        event: message_delta
        data: {"type":"message_delta","delta":{"stop_reason":"end_turn","stop_sequence":null},"usage":{"output_tokens":5}}

        event: message_stop
        data: {"type":"message_stop"}


        """;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Marks_the_system_prompt_and_the_newest_message_once_each()
    {
        var handler = new RecordingHandler(Stream);
        using var http = new HttpClient(handler);
        using var client = new AnthropicPromptCache(new AnthropicClient { ApiKey = "test", HttpClient = http, MaxRetries = 0 }.AsIChatClient("claude-sonnet-5-5"));
        List<ChatMessage> history = [new(ChatRole.System, "You are a terminal agent."), new(ChatRole.User, "First")];

        await client.GetStreamingResponseAsync(history, cancellationToken: Token).ToChatResponseAsync(Token);
        history.Add(new ChatMessage(ChatRole.Assistant, "Hi"));
        history.Add(new ChatMessage(ChatRole.User, "Second"));
        var response = await client.GetStreamingResponseAsync(history, cancellationToken: Token).ToChatResponseAsync(Token);

        using var body = JsonDocument.Parse(handler.Bodies[^1]);
        Assert.Equal(2, CountMarks(body.RootElement));
        Assert.True(Last(body.RootElement.GetProperty("system")).TryGetProperty("cache_control", out _));
        Assert.True(Last(body.RootElement.GetProperty("messages")[2].GetProperty("content")).TryGetProperty("cache_control", out _));
        Assert.Equal((2310L, 2000L), (response.Usage!.InputTokenCount, response.Usage.CachedInputTokenCount));
        Assert.Equal(300L, response.Usage.AdditionalCounts!["CacheCreationInputTokens"]);
    }

    private static JsonElement Last(JsonElement blocks) => blocks.ValueKind == JsonValueKind.Array ? blocks[blocks.GetArrayLength() - 1] : blocks;

    private static int CountMarks(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().Sum(property => (property.NameEquals("cache_control") ? 1 : 0) + CountMarks(property.Value)),
        JsonValueKind.Array => element.EnumerateArray().Sum(CountMarks),
        _ => 0,
    };

    private sealed class RecordingHandler(string stream) : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(stream, Encoding.UTF8, "text/event-stream") };
        }
    }
}
