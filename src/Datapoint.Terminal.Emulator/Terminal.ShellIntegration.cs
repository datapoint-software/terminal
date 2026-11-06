using Datapoint.Terminal.Emulator.ShellIntegration;

namespace Datapoint.Terminal.Emulator;

public sealed partial class Terminal
{
    public const int MaxCommandRecords = 10_000;

    private readonly List<CommandRecord> _commands = [];
    private readonly TimeProvider _time = TimeProvider.System;
    private CommandSource _nextCommandSource;

    public string? ShellIntegrationNonce { get; set; }

    public IReadOnlyList<CommandRecord> Commands => _commands;

    public CommandRecord? CurrentCommand => _commands.Count == 0 ? null : _commands[^1];

    public bool IsAtPrompt => CurrentCommand is { InputLine: not null, OutputLine: null };

    public void MarkNextCommandSource(CommandSource source)
    {
        lock (_sync)
        {
            _nextCommandSource = source;
        }
    }

    private long CursorLine => Primary.FirstVisibleLine + Cursor.Row;

    private void HandleShellIntegration(string argument)
    {
        if (IsAlternateScreen)
        {
            return;
        }

        var parts = argument.Split(';');
        switch (parts[0])
        {
            case "A":
                StartPrompt();
                break;
            case "B":
                StartInput();
                break;
            case "C":
                StartOutput();
                break;
            case "D":
                FinishCommand(parts);
                break;
            case "E":
                SetCommandLine(parts);
                break;
            case "P":
                SetProperty(parts);
                break;
        }
    }

    private void StartPrompt()
    {
        if (CurrentCommand is { IsRunning: true } running)
        {
            running.EndLine = CursorLine;
            running.FinishedAt = _time.GetUtcNow();
        }

        _commands.Add(new CommandRecord { PromptLine = CursorLine, WorkingDirectory = WorkingDirectory });
        if (_commands.Count <= MaxCommandRecords)
        {
            return;
        }

        _commands.RemoveAt(0);
    }

    private void StartInput()
    {
        if (CurrentCommand is not { InputLine: null } command)
        {
            return;
        }

        command.InputLine = CursorLine;
        command.InputColumn = Cursor.Column;
    }

    private void StartOutput()
    {
        if (CurrentCommand is not { OutputLine: null, InputLine: not null } command)
        {
            return;
        }

        command.CommandLine ??= ReadCommandLine(command);
        command.OutputLine = CursorLine;
        command.StartedAt = _time.GetUtcNow();
        command.Source = _nextCommandSource;
        _nextCommandSource = CommandSource.Unknown;
    }

    private string ReadCommandLine(CommandRecord command)
    {
        var firstRow = (int)(command.InputLine!.Value - Primary.FirstVisibleLine);
        if (firstRow < 0 || firstRow > Cursor.Row)
        {
            return string.Empty;
        }

        var text = new System.Text.StringBuilder();
        for (var row = firstRow; row <= Cursor.Row && row < Rows; row++)
        {
            var line = Screen[row].GetText();
            text.Append(row == firstRow ? SafeSubstring(line, command.InputColumn) : line);
        }

        return text.ToString().Trim();
    }

    private static string SafeSubstring(string text, int start) => start < text.Length ? text[start..] : string.Empty;

    private void FinishCommand(string[] parts)
    {
        if (CurrentCommand is not { IsFinished: false, OutputLine: not null } command)
        {
            return;
        }

        command.EndLine = CursorLine;
        command.FinishedAt = _time.GetUtcNow();
        command.ExitCode = parts.Length > 1 && int.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out var code) ? code : null;
    }

    private void SetCommandLine(string[] parts)
    {
        if (CurrentCommand is not { } command || parts.Length < 2)
        {
            return;
        }

        var isTrusted = ShellIntegrationNonce is not null && parts.Length > 2 && string.Equals(parts[2], ShellIntegrationNonce, StringComparison.Ordinal);
        command.CommandLine = UnescapeVsCode(parts[1]);
        command.CommandLineIsTrusted = isTrusted;
    }

    private void SetProperty(string[] parts)
    {
        if (parts.Length < 2 || !parts[1].StartsWith("Cwd=", StringComparison.Ordinal))
        {
            return;
        }

        SetWorkingDirectory(UnescapeVsCode(parts[1]["Cwd=".Length..]));
    }

    private static string UnescapeVsCode(string value)
    {
        if (!value.Contains('\\', StringComparison.Ordinal))
        {
            return value;
        }

        var text = new System.Text.StringBuilder(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            index = AppendUnescaped(value, index, text);
        }

        return text.ToString();
    }

    private static int AppendUnescaped(string value, int index, System.Text.StringBuilder text)
    {
        var isHexEscape = value[index] == '\\' && index + 3 < value.Length && value[index + 1] == 'x';
        if (isHexEscape && byte.TryParse(value.AsSpan(index + 2, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var code))
        {
            text.Append((char)code);
            return index + 3;
        }

        var isEscapedBackslash = value[index] == '\\' && index + 1 < value.Length && value[index + 1] == '\\';
        text.Append(value[index]);
        return isEscapedBackslash ? index + 1 : index;
    }
}
