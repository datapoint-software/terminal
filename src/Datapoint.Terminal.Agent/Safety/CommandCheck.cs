namespace Datapoint.Terminal.Agent.Safety;

/// <summary>Checks a command the model wants to type, before anyone is asked to approve it.</summary>
public static class CommandCheck
{
    /// <summary>
    /// Returns why the command can't be typed, or null when it can. Commands are one complete line with no control characters,
    /// so a model can't send escape sequences or a second, hidden command into the terminal.
    /// </summary>
    public static string? Problem(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return "The command is empty.";
        }

        if (command.Length > 4096)
        {
            return "The command is longer than 4096 characters; write a script file instead.";
        }

        if (command.Contains('\n', StringComparison.Ordinal) || command.Contains('\r', StringComparison.Ordinal))
        {
            return "Commands must be a single line. Join steps with && or ;, or send them one at a time.";
        }

        return command.Any(character => char.IsControl(character) && character != '\t')
            ? "The command contains control characters, which aren't allowed."
            : null;
    }
}
