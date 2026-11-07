namespace Datapoint.Terminal.Agent.Tests.Fakes;

internal sealed class FakeTerminal : IAgentTerminal
{
    private readonly Dictionary<long, CommandResult> _commands = [];
    private long _line;

    public event EventHandler? OutputArrived;

    public event EventHandler<Datapoint.Terminal.Emulator.LinesMovedEventArgs>? LinesMoved
    {
        add { }
        remove { }
    }

    public event EventHandler? Ended;

    public List<string> Typed { get; } = [];

    public List<string> Keys { get; } = [];

    public bool IsAlternateScreen { get; set; }

    public int Interrupts { get; private set; }

    public string PendingInput { get; set; } = string.Empty;

    public bool IsAtPrompt { get; set; } = true;

    public bool FinishesCommands { get; set; } = true;

    public Func<string, (string Output, int? ExitCode)> Run { get; set; } = command => ($"ran {command}", 0);

    public string? Session { get; set; }

    public TerminalState GetState() => new(HasShellIntegration: true, IsAtPrompt, PendingInput, IsAlternateScreen, "/home/user", _line) { Session = Session };

    public CommandResult? GetCommand(long outputLine) => _commands.GetValueOrDefault(outputLine);

    public string GetVisibleText() => "$ ";

    public void TypeCommand(string text)
    {
        Typed.Add(text);
        var (output, exitCode) = Run(text);
        _commands[_line] = new CommandResult(text, output, FinishesCommands, exitCode, IsAgentCommand: true);
        IsAtPrompt = FinishesCommands;
        _line += 2;
        OutputArrived?.Invoke(this, EventArgs.Empty);
    }

    public void FinishLast(string output, int exitCode)
    {
        var line = _line - 2;
        _commands[line] = _commands[line] with { Output = output, IsFinished = true, ExitCode = exitCode };
        IsAtPrompt = true;
        OutputArrived?.Invoke(this, EventArgs.Empty);
    }

    public string NewOutput { get; set; } = string.Empty;

    public void Advance(long lines) => _line += lines;

    public long NewOutputSince { get; private set; } = -1;

    public string GetTextSince(long line)
    {
        NewOutputSince = line;
        return NewOutput;
    }

    public void SendKeys(IReadOnlyList<string> parts) => Keys.Add(string.Concat(parts));

    public void Interrupt() => Interrupts++;

    public void End() => Ended?.Invoke(this, EventArgs.Empty);

    public void Print(string text)
    {
        NewOutput += text;
        OutputArrived?.Invoke(this, EventArgs.Empty);
    }
}
