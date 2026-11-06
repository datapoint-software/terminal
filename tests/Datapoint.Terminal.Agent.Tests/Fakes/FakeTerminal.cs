namespace Datapoint.Terminal.Agent.Tests.Fakes;

/// <summary>A terminal with shell integration where every typed command finishes at once with a scripted result.</summary>
internal sealed class FakeTerminal : IAgentTerminal
{
    private readonly Dictionary<long, CommandResult> _commands = [];
    private long _line;

    public event EventHandler? OutputArrived;

    public event EventHandler? Ended;

    public List<string> Typed { get; } = [];

    public int Interrupts { get; private set; }

    public string PendingInput { get; set; } = string.Empty;

    public bool IsAtPrompt { get; set; } = true;

    /// <summary>When false, typed commands keep running until <see cref="FinishLast"/>.</summary>
    public bool FinishesCommands { get; set; } = true;

    public Func<string, (string Output, int? ExitCode)> Run { get; set; } = command => ($"ran {command}", 0);

    public TerminalState GetState() => new(HasShellIntegration: true, IsAtPrompt, PendingInput, IsAlternateScreen: false, "/home/user", _line);

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

    public void Interrupt() => Interrupts++;

    public void End() => Ended?.Invoke(this, EventArgs.Empty);
}
