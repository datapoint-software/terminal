using Datapoint.Terminal.Agent.Safety;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Agent;

/// <summary>
/// The agent for one terminal: keeps the conversation, sends it to the provider, and carries out the tools the model calls.
/// One task runs at a time; <see cref="Interrupt"/> stops it, leaving any running command alone.
/// </summary>
public sealed partial class TerminalAgent : IDisposable
{
    private const string InterruptedResult = "Not completed: the user interrupted you. Wait for their instructions.";

    private volatile IChatClient _client;
    private readonly IAgentTerminal _terminal;
    private readonly IAgentUser _user;
    private readonly SecretRedactor _redactor;
    private readonly AgentOptions _options;
    private readonly List<ChatMessage> _history = [new(ChatRole.System, SystemPrompt.Text)];
    private readonly ChatOptions _chatOptions;
    private readonly Lock _sync = new();
    private CancellationTokenSource? _task;
    private AgentStatus _status;
    private volatile bool _isAwaitingTerminalInput;

    public TerminalAgent(IChatClient client, IAgentTerminal terminal, IAgentUser user, SecretRedactor redactor, AgentOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(terminal);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(redactor);
        _client = client;
        _terminal = terminal;
        _user = user;
        _redactor = redactor;
        _options = options ?? new AgentOptions();
        Mode = _options.DefaultMode;
        _chatOptions = new ChatOptions { Tools = CreateTools(), MaxOutputTokens = _options.MaxOutputTokens };
    }

    /// <summary>Switches provider or model; takes effect from the agent's next step, keeping the conversation.</summary>
    public void UseClient(IChatClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    /// <summary>Raised on a background thread for everything the chat pane shows.</summary>
    public event EventHandler<AgentEventArgs>? EventRaised;

    /// <summary>Applies from the next command. Auto lasts for one task, then falls back to the default.</summary>
    public ApprovalMode Mode { get; set; }

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

    /// <summary>
    /// True while the agent waits for the user to answer a prompt in the terminal (e.g. a password); typing then is the answer,
    /// not an interruption.
    /// </summary>
    public bool IsAwaitingTerminalInput => _isAwaitingTerminalInput;

    /// <summary>Gives the agent a task from the chat. Completes when the agent has finished, been interrupted, or failed.</summary>
    public Task SendAsync(string message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return RunTaskAsync(message, cancellationToken);
    }

    /// <summary>Resumes after an interruption; the agent is told to look at the terminal first, since the user may have used it.</summary>
    public Task ContinueAsync(CancellationToken cancellationToken = default) => RunTaskAsync(
        "Continue the task. I may have used the terminal while you were stopped: use read_screen to check its state before going on.",
        cancellationToken);

    /// <summary>Stops the current task. Anything already running in the terminal keeps running.</summary>
    public void Interrupt()
    {
        lock (_sync)
        {
            _task?.Cancel();
        }
    }

    /// <summary>Called for every keystroke or paste in the terminal: interrupts the agent unless it's waiting for that input.</summary>
    public void NotifyUserTyped()
    {
        if (_isAwaitingTerminalInput)
        {
            return;
        }

        Interrupt();
    }

    /// <summary>Forgets the conversation, keeping the instructions.</summary>
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
            _history.Add(new ChatMessage(ChatRole.User, message));
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
            _isAwaitingTerminalInput = false;
            Mode = Mode == ApprovalMode.Auto ? _options.DefaultMode : Mode;
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

    /// <summary>Keeps what the user already saw of an interrupted reply, so the model knows what it said.</summary>
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

    /// <summary>Gives every tool call without a result an "interrupted" one, so the conversation stays valid for the provider.</summary>
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

    private void SetStatus(AgentStatus status)
    {
        _status = status;
        Raise(new AgentEventArgs(AgentEventKind.StatusChanged, status.ToString()) { Status = status });
    }

    private void Raise(AgentEventArgs agentEvent) => EventRaised?.Invoke(this, agentEvent);
}
