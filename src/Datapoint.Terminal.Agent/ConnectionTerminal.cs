using System.Text;
using Datapoint.Terminal.Emulator.ShellIntegration;
using Datapoint.Terminal.Sessions;
using EmulatorTerminal = Datapoint.Terminal.Emulator.Terminal;

namespace Datapoint.Terminal.Agent;

// Everything the agent types goes through the same input as the keyboard.
public sealed class ConnectionTerminal : IAgentTerminal
{
    private readonly TerminalConnection _connection;

    public ConnectionTerminal(TerminalConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
        _connection.OutputArrived += (_, _) => OutputArrived?.Invoke(this, EventArgs.Empty);
        _connection.Ended += (_, _) => Ended?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? OutputArrived;

    public event EventHandler? Ended;

    private EmulatorTerminal Emulator => _connection.Terminal;

    public TerminalState GetState()
    {
        lock (Emulator.SyncRoot)
        {
            var current = Emulator.CurrentCommand;
            var nextLine = Emulator.Primary.FirstVisibleLine + Emulator.Cursor.Row;

            // Inside ssh or another session the shell's reports describe the outer shell, so they're set aside.
            if (NestedIn(current) is { } session)
            {
                return new TerminalState(HasShellIntegration: false, IsAtPrompt: false, string.Empty, Emulator.IsAlternateScreen, WorkingDirectory: null, nextLine)
                {
                    Session = session,
                    IsInputHidden = !Emulator.IsAlternateScreen && _connection.IsInputHidden == true,
                };
            }

            var pending = current is { InputLine: { } inputLine, OutputLine: null } ? ReadPendingInput(inputLine, current.InputColumn) : string.Empty;
            return new TerminalState(
                HasShellIntegration: current is not null,
                Emulator.IsAtPrompt,
                pending,
                Emulator.IsAlternateScreen,
                Emulator.WorkingDirectory,
                nextLine)
            {
                IsInputHidden = !Emulator.IsAlternateScreen && _connection.IsInputHidden == true,
            };
        }
    }

    public CommandResult? GetCommand(long outputLine)
    {
        lock (Emulator.SyncRoot)
        {
            var record = FindCommand(outputLine);
            if (record is null)
            {
                return null;
            }

            // The shell reports the end of a command where its next prompt will start, so that line isn't output.
            var last = record.EndLine is { } end ? end - 1 : Emulator.Primary.FirstVisibleLine + Emulator.Cursor.Row;
            var output = last >= record.OutputLine!.Value ? Emulator.GetLineText(record.OutputLine.Value, last) : string.Empty;
            return new CommandResult(record.CommandLine, output, record.IsFinished, record.ExitCode, record.Source == CommandSource.Agent);
        }
    }

    public string GetVisibleText() => Emulator.GetVisibleText();

    public string GetTextSince(long line)
    {
        lock (Emulator.SyncRoot)
        {
            var last = Emulator.Primary.FirstVisibleLine + Emulator.Cursor.Row;
            return line > last ? string.Empty : Emulator.GetLineText(line, last);
        }
    }

    public void TypeCommand(string text)
    {
        // Inside a nested session the outer shell sees no new command, so marking one would label the user's next one.
        bool isNested;
        lock (Emulator.SyncRoot)
        {
            isNested = NestedIn(Emulator.CurrentCommand) is not null;
        }

        if (!isNested)
        {
            Emulator.MarkNextCommandSource(CommandSource.Agent);
        }

        _connection.Send(Encoding.UTF8.GetBytes(text + "\r"));
    }

    public void SendKeys(IReadOnlyList<string> parts)
    {
        bool applicationCursor;
        lock (Emulator.SyncRoot)
        {
            applicationCursor = Emulator.Modes.ApplicationCursorKeys;
        }

        _connection.Send(Keys.KeyNotation.Encode(parts, applicationCursor));
    }

    public void Interrupt() => _connection.Send([0x03]);

    private static string? NestedIn(CommandRecord? current) => current is { IsRunning: true } ? NestedSession.Describe(current.CommandLine) : null;

    private CommandRecord? FindCommand(long outputLine)
    {
        var commands = Emulator.Commands;
        for (var index = commands.Count - 1; index >= 0; index--)
        {
            var record = commands[index];
            if (record.OutputLine is { } line && line >= outputLine)
            {
                return record;
            }

            if (record.PromptLine < outputLine)
            {
                break;
            }
        }

        return null;
    }

    private string ReadPendingInput(long inputLine, int inputColumn)
    {
        var cursorLine = Emulator.Primary.FirstVisibleLine + Emulator.Cursor.Row;
        var text = Emulator.GetLineText(inputLine, Math.Max(inputLine, cursorLine));
        return (inputColumn < text.Length ? text[inputColumn..] : string.Empty).Trim();
    }
}
