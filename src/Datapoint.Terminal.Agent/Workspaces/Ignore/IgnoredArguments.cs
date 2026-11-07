using System.Text;

namespace Datapoint.Terminal.Agent.Workspaces.Ignore;

// Finds a file listed in .terminalignore among a command's words, so the command asks first even in Auto. It's a best guess:
// words are split roughly as a shell would, paths are taken relative to the terminal's folder, and a glob in a path's last
// part is matched against the files there. A file reached any other way (a variable, a script, `grep -r` over a folder) isn't
// found, which is why the user still sees and approves what runs.
public static class IgnoredArguments
{
    private const int MaxGlobMatches = 200;
    private static readonly char[] s_globCharacters = ['*', '?', '['];

    public static string? Find(string command, string workingDirectory, string home, Func<string, bool> isIgnored)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(isIgnored);
        return Words(command)
            .SelectMany(Candidates)
            .SelectMany(word => Paths(word, workingDirectory, home))
            .FirstOrDefault(isIgnored);
    }

    // Quotes group, a backslash escapes outside single quotes, and operators end a word.
    public static IReadOnlyList<string> Words(string command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var words = new List<string>();
        var word = new StringBuilder();
        var quote = '\0';
        for (var index = 0; index < command.Length; index++)
        {
            var character = command[index];
            if (quote != '\0')
            {
                index = InQuote(command, index, word, ref quote);
            }
            else if (character is '\'' or '"')
            {
                quote = character;
            }
            else if (character == '\\' && index + 1 < command.Length)
            {
                word.Append(command[++index]);
            }
            else if (char.IsWhiteSpace(character) || character is ';' or '|' or '&' or '<' or '>' or '(' or ')' or '`')
            {
                Flush(words, word);
            }
            else
            {
                word.Append(character);
            }
        }

        Flush(words, word);
        return words;
    }

    private static int InQuote(string command, int index, StringBuilder word, ref char quote)
    {
        var character = command[index];
        if (character == quote)
        {
            quote = '\0';
        }
        else if (quote == '"' && character == '\\' && index + 1 < command.Length)
        {
            word.Append(command[++index]);
        }
        else
        {
            word.Append(character);
        }

        return index;
    }

    private static void Flush(List<string> words, StringBuilder word)
    {
        if (word.Length == 0)
        {
            return;
        }

        words.Add(word.ToString());
        word.Clear();
    }

    // An option's value (--file=.env) or an assignment's (FILE=.env) can name a file too; a bare option can't.
    private static IEnumerable<string> Candidates(string word)
    {
        var equals = word.IndexOf('=', StringComparison.Ordinal);
        if (equals >= 0 && equals < word.Length - 1)
        {
            yield return word[(equals + 1)..];
        }

        if (word.StartsWith('-'))
        {
            yield break;
        }

        yield return word;
    }

    private static List<string> Paths(string word, string workingDirectory, string home)
    {
        string full;
        try
        {
            var expanded = string.Equals(word, "~", StringComparison.Ordinal) ? home
                : word.StartsWith("~/", StringComparison.Ordinal) ? Path.Combine(home, word[2..])
                : word;
            full = Path.GetFullPath(expanded, workingDirectory);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return [];
        }

        return full.IndexOfAny(s_globCharacters) < 0 ? [full] : Expand(full);
    }

    private static List<string> Expand(string pattern)
    {
        var folder = Path.GetDirectoryName(pattern);
        var name = Path.GetFileName(pattern);
        if (folder is null || folder.IndexOfAny(s_globCharacters) >= 0 || name.Contains('[', StringComparison.Ordinal))
        {
            return [];
        }

        try
        {
            return Directory.Exists(folder) ? [.. Directory.EnumerateFiles(folder, name).Take(MaxGlobMatches)] : [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return [];
        }
    }
}
