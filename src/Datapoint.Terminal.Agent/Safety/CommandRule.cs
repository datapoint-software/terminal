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

    // The shell drops quotes and backslashes before the program sees the word, so '-o' is -o.
    private bool IsWriting(string word)
    {
        var unquoted = word.Replace("'", string.Empty, StringComparison.Ordinal).Replace("\"", string.Empty, StringComparison.Ordinal)
            .Replace("\\", string.Empty, StringComparison.Ordinal);
        return Writes.Any(option => Matches(unquoted, option));
    }

    private bool Matches(string word, string option)
    {
        if (IsWindows)
        {
            return string.Equals(word, option, StringComparison.OrdinalIgnoreCase);
        }

        if (option.EndsWith('*'))
        {
            return word.StartsWith(option[..^1], StringComparison.Ordinal) || IsAbbreviation(word, option[..^1]);
        }

        return string.Equals(word, option, StringComparison.Ordinal) || word.StartsWith(option + "=", StringComparison.Ordinal)
            || IsAbbreviation(word, option) || IsShortOptionIn(word, option);
    }

    // Programs reading their options with getopt_long take any unambiguous start of a long one, e.g. --out for --output.
    private static bool IsAbbreviation(string word, string option)
    {
        var name = word.Split('=')[0];
        return option.StartsWith("--", StringComparison.Ordinal) && name.Length >= 3 && option.StartsWith(name, StringComparison.Ordinal);
    }

    // A short option counts with its value attached (-ofile) or among others (-uo file).
    private static bool IsShortOptionIn(string word, string option) =>
        option.Length == 2 && option[0] == '-' && word.Length > 2 && word[0] == '-' && word[1] != '-'
        && (word[1] == option[1] || (word.Skip(1).All(char.IsAsciiLetterOrDigit) && word.Contains(option[1], StringComparison.Ordinal)));
}
