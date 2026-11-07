using Datapoint.Terminal.Agent.Safety;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Agent;

public sealed partial class TerminalAgent : IDisposable
{
    private const string InterruptedResult = "Not completed: the user interrupted you. Wait for their instructions.";

    private volatile IChatClient _client;
    private readonly List<AgentTerminal> _terminals = [];
    private readonly IAgentUser _user;
    private readonly SecretRedactor _redactor;
    private readonly AgentOptions _options;
    private readonly List<ChatMessage> _history = [new(ChatRole.System, SystemPrompt.Text)];
    private readonly ChatOptions _chatOptions;
    private readonly Lock _sync = new();
    private CancellationTokenSource? _task;
    private AgentStatus _status;

    // Can change with the model; read when output is cut.
    public int OutputLimit { get; set; }

    // Whether there's a conversation a model switch would carry over.
    public bool HasConversation
    {
        get
        {
            lock (_sync)
            {
                return _history.Count > 1;
            }
        }
    }

    public TerminalAgent(IChatClient client, IAgentUser user, SecretRedactor redactor, AgentOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(redactor);
        _client = client;
        _user = user;
        _redactor = redactor;
        _options = options ?? new AgentOptions();
        OutputLimit = _options.MaxOutputCharacters;
        _chatOptions = new ChatOptions { Tools = CreateTools(), MaxOutputTokens = _options.MaxOutputTokens };
    }

    public void UseClient(IChatClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    // Raised on a background thread.
    public event EventHandler<AgentEventArgs>? EventRaised;

    public IReadOnlyList<AgentTerminal> Terminals
    {
        get
        {
            lock (_sync)
            {
                return [.. _terminals];
            }
        }
    }

    public AgentStatus Status => _status;

    public bool IsBusy
    {
        get
        {
            lock (_sync)
            {
                return _task is not null;
            }
        }
    }

    public void AddTerminal(AgentTerminal terminal)
    {
        ArgumentNullException.ThrowIfNull(terminal);
        lock (_sync)
        {
            if (_terminals.Any(item => string.Equals(item.Name, terminal.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException($"There is already a terminal called {terminal.Name}.", nameof(terminal));
            }

            terminal.SeenUpTo = terminal.Terminal.GetState().NextOutputLine;
            _terminals.Add(terminal);
        }
    }

    public void RemoveTerminal(AgentTerminal terminal)
    {
        lock (_sync)
        {
            _terminals.Remove(terminal);
        }
    }

    public Task SendAsync(string message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return RunTaskAsync(message, cancellationToken);
    }

    // The agent is told to look at the terminal first, since the user may have used it.
    public Task ContinueAsync(CancellationToken cancellationToken = default) => RunTaskAsync(
        "Continue the task. I may have used the terminal while you were stopped: use read_screen to check its state before going on.",
        cancellationToken);

    // Anything already running in the terminal keeps running: stopping it is the user's call.
    public void Interrupt()
    {
        lock (_sync)
        {
            _task?.Cancel();
        }
    }

    // Typing is the awaited answer only in the terminal the agent asked the user to type into; anywhere else it interrupts.
    public void NotifyUserTyped(AgentTerminal terminal)
    {
        ArgumentNullException.ThrowIfNull(terminal);
        if (terminal.IsAwaitingInput)
        {
            return;
        }

        Interrupt();
    }

    public void ClearConversation()
    {
        lock (_sync)
        {
            if (_task is not null)
            {
                throw new InvalidOperationException("The agent is working; interrupt it first.");
            }

            _history.RemoveRange(1, _history.Count - 1);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _task?.Cancel();
        }
    }

    private async Task RunTaskAsync(string message, CancellationToken cancellationToken)
    {
        using var task = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (_sync)
        {
            if (_task is not null)
            {
                throw new InvalidOperationException("The agent is already working on a task.");
            }

            _task = task;
        }

        try
        {
            var opening = await StartConversationAsync(message, task.Token).ConfigureAwait(false);
            _history.Add(new ChatMessage(ChatRole.User, DescribeTerminals() + NewOutputBlock() + opening));
            await RunStepsAsync(task.Token).ConfigureAwait(false);
            SetStatus(AgentStatus.Idle);
        }
        catch (OperationCanceledException) when (task.IsCancellationRequested)
        {
            CloseUnansweredCalls();
            Raise(new AgentEventArgs(AgentEventKind.Notice, "Interrupted. Tell me how to go on, or continue."));
            SetStatus(AgentStatus.Interrupted);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            CloseUnansweredCalls();
            Raise(new AgentEventArgs(AgentEventKind.Notice, "The request to the AI provider failed: " + exception.Message));
            SetStatus(AgentStatus.Idle);
        }
        finally
        {
            EndTask();
            lock (_sync)
            {
                _task = null;
            }
        }
    }

    private async Task RunStepsAsync(CancellationToken cancellationToken)
    {
        for (var step = 0; step < _options.MaxStepsPerTask; step++)
        {
            SetStatus(AgentStatus.Thinking);
            var response = await GetResponseAsync(cancellationToken).ConfigureAwait(false);
            _history.AddMessages(response);
            if (!string.IsNullOrWhiteSpace(response.Text))
            {
                Raise(new AgentEventArgs(AgentEventKind.Reply, response.Text));
            }

            var calls = response.Messages.SelectMany(item => item.Contents).OfType<FunctionCallContent>().ToList();
            if (calls.Count == 0)
            {
                return;
            }

            // One message holds all of a step's results, as providers expect; it's in the history from the start so that an
            // interruption can complete it.
            var results = new ChatMessage(ChatRole.Tool, []);
            _history.Add(results);
            foreach (var call in calls)
            {
                var result = await InvokeToolAsync(call, cancellationToken).ConfigureAwait(false);
                results.Contents.Add(new FunctionResultContent(call.CallId, result));
            }
        }

        Raise(new AgentEventArgs(AgentEventKind.Notice, $"Stopped after {_options.MaxStepsPerTask} steps. Tell me whether to go on."));
    }

    private async Task<ChatResponse> GetResponseAsync(CancellationToken cancellationToken)
    {
        var updates = new List<ChatResponseUpdate>();
        try
        {
            await foreach (var update in _client.GetStreamingResponseAsync(_history, _chatOptions, cancellationToken).ConfigureAwait(false))
            {
                updates.Add(update);
                var text = update.Text;
                if (!string.IsNullOrEmpty(text))
                {
                    Raise(new AgentEventArgs(AgentEventKind.TextDelta, text));
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            KeepPartialReply(updates);
            throw;
        }

        return updates.ToChatResponse();
    }

    // Kept so the model knows what the user already saw of an interrupted reply.
    private void KeepPartialReply(List<ChatResponseUpdate> updates)
    {
        var text = string.Concat(updates.Select(update => update.Text));
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _history.Add(new ChatMessage(ChatRole.Assistant, text + " [interrupted]"));
    }

    private async Task<string> InvokeToolAsync(FunctionCallContent call, CancellationToken cancellationToken)
    {
        var tool = _chatOptions.Tools!.OfType<AIFunction>().FirstOrDefault(item => string.Equals(item.Name, call.Name, StringComparison.Ordinal));
        if (tool is null)
        {
            return $"There is no tool called '{call.Name}'.";
        }

        var arguments = call.Arguments is null ? new AIFunctionArguments(StringComparer.Ordinal) : new AIFunctionArguments(call.Arguments, StringComparer.Ordinal);
        var result = await tool.InvokeAsync(arguments, cancellationToken).ConfigureAwait(false);
        return result?.ToString() ?? string.Empty;
    }

    // Every tool call needs a result, or the conversation isn't valid for the provider.
    private void CloseUnansweredCalls()
    {
        var answered = _history.SelectMany(message => message.Contents).OfType<FunctionResultContent>().Select(result => result.CallId).ToHashSet(StringComparer.Ordinal);
        var unanswered = _history.SelectMany(message => message.Contents).OfType<FunctionCallContent>().Where(call => !answered.Contains(call.CallId)).ToList();
        if (unanswered.Count == 0)
        {
            return;
        }

        var results = _history[^1].Role == ChatRole.Tool ? _history[^1] : new ChatMessage(ChatRole.Tool, []);
        foreach (var call in unanswered)
        {
            results.Contents.Add(new FunctionResultContent(call.CallId, InterruptedResult));
        }

        if (ReferenceEquals(results, _history[^1]))
        {
            return;
        }

        _history.Add(results);
    }

    // Auto and an approved plan last for one task; then each terminal falls back to its default.
    private void EndTask()
    {
        MarkAllSeen();
        _plan = null;
        foreach (var terminal in Terminals)
        {
            terminal.IsAwaitingInput = false;
            terminal.Mode = terminal.Mode == ApprovalMode.Auto ? terminal.DefaultMode : terminal.Mode;
        }
    }

    private string DescribeTerminals()
    {
        var terminals = Terminals;
        var open = terminals.Count == 0
            ? "[No terminals are open.]\n"
            : "[Open terminals: " + string.Join(", ", terminals.Select(item => Named(item.Name, item.Environment))) + "]\n";
        var connections = TerminalOpener?.Connections ?? [];
        var openable = connections.Count == 0
            ? string.Empty
            : "[Connections open_terminal can open: " + string.Join(", ", connections.Select(item => Named(item.Name, item.Environment))) + "]\n";
        return open + openable + DescribeHostDocs();
    }

    private static string Named(string name, string? environment) => $"\"{name}\"" + (environment is null ? string.Empty : $" ({environment})");

    private void SetStatus(AgentStatus status, AgentTerminal? terminal = null)
    {
        _status = status;
        Raise(new AgentEventArgs(AgentEventKind.StatusChanged, status.ToString()) { Status = status, Terminal = terminal?.Name });
    }

    private void Raise(AgentEventArgs agentEvent) => EventRaised?.Invoke(this, agentEvent);
}
