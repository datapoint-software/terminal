namespace Datapoint.Terminal.Agent;

public sealed class AgentTerminal
{
    private volatile int _mode;
    private volatile bool _isAwaitingInput;

    public AgentTerminal(string name, IAgentTerminal terminal, ApprovalMode defaultMode, string? environment = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(terminal);
        Name = name;
        Terminal = terminal;
        DefaultMode = defaultMode;
        Environment = environment;
        _mode = (int)defaultMode;
    }

    public event EventHandler? ModeChanged;

    public string Name { get; }

    public IAgentTerminal Terminal { get; }

    public string? Environment { get; }

    // On another machine, whose files the project's .terminalignore doesn't describe.
    public bool IsRemote { get; init; }

    // The connection the terminal was opened for, which history is kept under.
    public string? Connection { get; init; }

    public ApprovalMode DefaultMode { get; }

    // Exact file access on the terminal's machine, beside the terminal; null where it isn't available, e.g. on Windows for now.
    public Files.IHostFiles? Files { get; init; }

    private long _seenUpTo;

    // The first line of output the agent hasn't been given: where the last task ended, or where the terminal was when it was
    // added. Output after it goes with the user's next message (product.md, Output sent to the provider).
    public long SeenUpTo
    {
        get => Interlocked.Read(ref _seenUpTo);
        set => Interlocked.Exchange(ref _seenUpTo, value);
    }

    public ApprovalMode Mode
    {
        get => (ApprovalMode)_mode;
        set
        {
            if (_mode == (int)value)
            {
                return;
            }

            _mode = (int)value;
            ModeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool IsAwaitingInput
    {
        get => _isAwaitingInput;
        internal set => _isAwaitingInput = value;
    }

    public string Label => Environment is null ? Name : $"{Name} ({Environment})";

    internal long? RunningCommandLine { get; set; }
}
