namespace Datapoint.Terminal.Agent.Safety;

// One read-only command from ReadOnlyCommands.jsonc, or one the user taught: the words it starts with, and what may follow.
internal sealed record CommandRule(string Command, IReadOnlyList<string> Writes, bool IsExact = false, bool IsWindows = false)
{
    public int Length { get; } = Command.Split(' ').Length;

    // Whether the words after the command's own keep it reading.
    public bool Permits(string part, IReadOnlyList<string> rest)
    {
        // PowerShell runs what's in braces and parentheses, wherever they appear.
        if (IsWindows && (part.Contains('(', StringComparison.Ordinal) || part.Contains('{', StringComparison.Ordinal)))
        {
            return false;
        }

        if (IsExact && rest.Any(word => !IsOption(word)))
        {
            return false;
        }

        return !rest.Any(IsWriting);
    }

    private bool IsOption(string word) => word.StartsWith('-') || (IsWindows && word.StartsWith('/'));

    private bool IsWriting(string word) => Writes.Any(option => Matches(word, option));

    private bool Matches(string word, string option)
    {
        if (IsWindows)
        {
            return string.Equals(word, option, StringComparison.OrdinalIgnoreCase);
        }

        if (option.EndsWith('*'))
        {
            return word.StartsWith(option[..^1], StringComparison.Ordinal);
        }

        return string.Equals(word, option, StringComparison.Ordinal) || word.StartsWith(option + "=", StringComparison.Ordinal);
    }
}
