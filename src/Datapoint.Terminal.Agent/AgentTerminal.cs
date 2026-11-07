namespace Datapoint.Terminal.Agent;

public sealed class AgentTerminal
{
    private volatile bool _isAwaitingInput;

    public AgentTerminal(string name, IAgentTerminal terminal, string? environment = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(terminal);
        Name = name;
        Terminal = terminal;
        Environment = environment;
        terminal.LinesMoved += (_, e) =>
        {
            SeenUpTo = e.Map.Map(SeenUpTo);
            RunningCommandLine = RunningCommandLine is { } running ? e.Map.Map(running) : null;
        };
    }

    public string Name { get; }

    public IAgentTerminal Terminal { get; }

    public string? Environment { get; }

    // On another machine, whose files the workspace's .terminalignore doesn't describe.
    public bool IsRemote { get; init; }

    // What the terminal's shell runs in when that isn't a machine, e.g. "Docker container", so the agent knows.
    public string? Place { get; init; }

    // The connection the terminal was opened for, which history is kept under.
    public string? Connection { get; init; }

    // Exact file access on the terminal's machine, beside the terminal; null where it isn't available, e.g. on Windows for now.
    public Files.IHostFiles? Files { get; init; }

    private long _seenUpTo;
    private long _runningCommandLine = -1;

    // The first line of output the agent hasn't been given: where the last task ended, or where the terminal was when it was
    // added. Output after it goes with the user's next message (product.md, Output sent to the provider).
    public long SeenUpTo
    {
        get => Interlocked.Read(ref _seenUpTo);
        set => Interlocked.Exchange(ref _seenUpTo, value);
    }

    public bool IsAwaitingInput
    {
        get => _isAwaitingInput;
        internal set => _isAwaitingInput = value;
    }

    public string Label => Environment is null ? Name : $"{Name} ({Environment})";

    // Set and moved under different threads, so it's read and written whole.
    internal long? RunningCommandLine
    {
        get => Interlocked.Read(ref _runningCommandLine) is var line && line < 0 ? null : line;
        set => Interlocked.Exchange(ref _runningCommandLine, value ?? -1);
    }

    internal int? RunningPlanStep { get; set; }
}
