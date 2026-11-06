using Datapoint.Terminal.Agent;

namespace Datapoint.Terminal.Tests.Chat;

internal sealed class InstantTerminal : IAgentTerminal
{
    private readonly Dictionary<long, CommandResult> _commands = [];
    private long _line;

    public event EventHandler? OutputArrived;

    public event EventHandler? Ended
    {
        add { }
        remove { }
    }

    public TerminalState GetState() => new(HasShellIntegration: true, IsAtPrompt: true, PendingInput: string.Empty, IsAlternateScreen: false, WorkingDirectory: null, _line);

    public CommandResult? GetCommand(long outputLine) => _commands.GetValueOrDefault(outputLine);

    public string GetVisibleText() => string.Empty;

    public void TypeCommand(string text)
    {
        _commands[_line] = new CommandResult(text, "ok", IsFinished: true, ExitCode: 0, IsAgentCommand: true);
        _line++;
        OutputArrived?.Invoke(this, EventArgs.Empty);
    }

    public void Interrupt()
    {
    }
}
