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

    public ApprovalMode DefaultMode { get; }

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
