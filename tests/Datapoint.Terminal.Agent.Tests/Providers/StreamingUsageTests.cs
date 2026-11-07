using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using Anthropic;
using Datapoint.Terminal.Agent.Usage;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Datapoint.Terminal.Agent.Tests.Providers;

// The usage display counts the tokens each provider reports while streaming; these feed each SDK's adapter a stream in the
// provider's documented format and check the counts survive to the combined response.
public sealed class StreamingUsageTests
{
    private const string AnthropicStream = """
        event: message_start
        data: {"type":"message_start","message":{"id":"msg_1","type":"message","role":"assistant","model":"claude-opus-5-5","content":[],"stop_reason":null,"stop_sequence":null,"usage":{"input_tokens":120,"output_tokens":1,"cache_creation_input_tokens":200,"cache_read_input_tokens":1000}}}

        event: content_block_start
        data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

        event: content_block_delta
        data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Hello"}}

        event: content_block_stop
        data: {"type":"content_block_stop","index":0}

        event: message_delta
        data: {"type":"message_delta","delta":{"stop_reason":"end_turn","stop_sequence":null},"usage":{"output_tokens":15}}

        event: message_stop
        data: {"type":"message_stop"}


        """;

    private const string ResponsesStream = """
        event: response.created
        data: {"type":"response.created","sequence_number":0,"response":{"id":"resp_1","object":"response","created_at":1,"status":"in_progress","model":"gpt-5","output":[],"parallel_tool_calls":true,"tool_choice":"auto","tools":[]}}

        event: response.output_text.delta
        data: {"type":"response.output_text.delta","sequence_number":1,"item_id":"msg_1","output_index":0,"content_index":0,"delta":"Hello"}

        event: response.completed
        data: {"type":"response.completed","sequence_number":2,"response":{"id":"resp_1","object":"response","created_at":1,"status":"completed","model":"gpt-5","output":[{"type":"message","id":"msg_1","status":"completed","role":"assistant","content":[{"type":"output_text","text":"Hello","annotations":[]}]}],"parallel_tool_calls":true,"tool_choice":"auto","tools":[],"usage":{"input_tokens":120,"input_tokens_details":{"cached_tokens":0},"output_tokens":15,"output_tokens_details":{"reasoning_tokens":0},"total_tokens":135}}}


        """;

    private const string CompletionsStream = """
        data: {"id":"c1","object":"chat.completion.chunk","created":1,"model":"llama","choices":[{"index":0,"delta":{"role":"assistant","content":"Hello"},"finish_reason":null}]}

        data: {"id":"c1","object":"chat.completion.chunk","created":1,"model":"llama","choices":[{"index":0,"delta":{},"finish_reason":"stop"}]}

        data: {"id":"c1","object":"chat.completion.chunk","created":1,"model":"llama","choices":[],"usage":{"prompt_tokens":120,"completion_tokens":15,"total_tokens":135}}

        data: [DONE]


        """;

    [Fact]
    public async Task Anthropic_reports_input_output_and_cached_tokens()
    {
        var handler = new CannedHandler(AnthropicStream);
        var client = new AnthropicClient { ApiKey = "test", HttpClient = new HttpClient(handler), MaxRetries = 0 }.AsIChatClient("claude-opus-5-5");

        var usage = await StreamAsync(client);

        var tokens = TokenUsage.From(usage);

        Assert.Equal(new TokenUsage(1320, 15) { CacheRead = 1000, CacheWrite = 200 }, tokens);
    }

#pragma warning disable OPENAI001
    [Fact]
    public async Task OpenAI_responses_report_input_and_output_tokens()
    {
        var handler = new CannedHandler(ResponsesStream);
        var client = new OpenAIClient(new ApiKeyCredential("test"), Options(handler, null)).GetResponsesClient().AsIChatClient("gpt-5");

        var usage = await StreamAsync(client);

        Assert.Equal(120, usage?.InputTokenCount);
        Assert.Equal(15, usage?.OutputTokenCount);
    }
#pragma warning restore OPENAI001

    // Chat Completions only sends usage when asked to with stream_options.include_usage, which a compatible server has to
    // support; the request has to ask for it.
    [Fact]
    public async Task Compatible_servers_are_asked_for_usage_and_it_is_kept()
    {
        var handler = new CannedHandler(CompletionsStream);
        var client = new OpenAIClient(new ApiKeyCredential("none"), Options(handler, new Uri("http://localhost:11434/v1"))).GetChatClient("llama").AsIChatClient();

        var usage = await StreamAsync(client);

        Assert.Contains("\"include_usage\":true", handler.LastRequest, StringComparison.Ordinal);
        Assert.Equal(120, usage?.InputTokenCount);
        Assert.Equal(15, usage?.OutputTokenCount);
    }

    private static OpenAIClientOptions Options(HttpMessageHandler handler, Uri? endpoint) => new()
    {
        Transport = new HttpClientPipelineTransport(new HttpClient(handler)),
        Endpoint = endpoint,
        RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
    };

    private static async Task<UsageDetails?> StreamAsync(IChatClient client)
    {
        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "Hi")]))
        {
            updates.Add(update);
        }

        return updates.ToChatResponse().Usage;
    }

    private sealed class CannedHandler(string stream) : HttpMessageHandler
    {
        public string LastRequest { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var body = stream.Replace("\r\n", "\n", StringComparison.Ordinal);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "text/event-stream") };
        }
    }
}
