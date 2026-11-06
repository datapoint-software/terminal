namespace Datapoint.Terminal.Sessions.Ssh.Config;

// OpenSSH's pattern rules: * and ? wildcards, case-insensitive, and a list matches when any pattern does and no negated
// (!) pattern does.
public static class SshPattern
{
    public static bool IsMatch(string text, string pattern)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(pattern);
        return IsMatch(text.AsSpan(), pattern.AsSpan());
    }

    public static bool IsListMatch(string text, IEnumerable<string> patterns)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        var matched = false;
        foreach (var pattern in patterns.Where(pattern => pattern.Length > 0))
        {
            if (pattern[0] == '!')
            {
                if (IsMatch(text, pattern[1..]))
                {
                    return false;
                }

                continue;
            }

            matched |= IsMatch(text, pattern);
        }

        return matched;
    }

    public static bool HasWildcard(string pattern) => pattern.AsSpan().IndexOfAny("*?!") >= 0;

    private static bool IsMatch(ReadOnlySpan<char> text, ReadOnlySpan<char> pattern)
    {
        var textIndex = 0;
        var patternIndex = 0;
        var starPattern = -1;
        var starText = 0;
        while (textIndex < text.Length)
        {
            if (patternIndex < pattern.Length && (pattern[patternIndex] == '?' || char.ToLowerInvariant(pattern[patternIndex]) == char.ToLowerInvariant(text[textIndex])))
            {
                textIndex++;
                patternIndex++;
            }
            else if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                starPattern = patternIndex++;
                starText = textIndex;
            }
            else if (starPattern >= 0)
            {
                patternIndex = starPattern + 1;
                textIndex = ++starText;
            }
            else
            {
                return false;
            }
        }

        while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
        {
            patternIndex++;
        }

        return patternIndex == pattern.Length;
    }
}
