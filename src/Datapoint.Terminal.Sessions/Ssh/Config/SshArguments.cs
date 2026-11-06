using System.Text;

namespace Datapoint.Terminal.Sessions.Ssh.Config;

// Splits a line into arguments as OpenSSH does: whitespace separates them, double or single quotes keep spaces, a backslash
// escapes a quote, a backslash or a space, and a # at the start of an argument begins a comment.
internal sealed class SshArguments
{
    private readonly List<string> _arguments = [];
    private readonly StringBuilder _current = new();
    private bool _hasArgument;
    private char? _quote;

    public static IReadOnlyList<string> Split(string line)
    {
        var splitter = new SshArguments();
        for (var index = 0; index < line.Length; index++)
        {
            if (splitter.Read(line, ref index))
            {
                break;
            }
        }

        splitter.Flush();
        return splitter._arguments;
    }

    private bool Read(string line, ref int index)
    {
        var character = line[index];
        if (_quote is null && character == '#' && !_hasArgument)
        {
            return true;
        }

        if (character == '\\' && index + 1 < line.Length && line[index + 1] is '"' or '\'' or '\\' or ' ')
        {
            Append(line[++index]);
        }
        else if (!TryQuote(character))
        {
            Accept(character);
        }

        return false;
    }

    private bool TryQuote(char character)
    {
        if (_quote is null && character is '"' or '\'')
        {
            _quote = character;
            _hasArgument = true;
            return true;
        }

        if (_quote != character)
        {
            return false;
        }

        _quote = null;
        return true;
    }

    private void Accept(char character)
    {
        if (_quote is null && char.IsWhiteSpace(character))
        {
            Flush();
            return;
        }

        Append(character);
    }

    private void Append(char character)
    {
        _current.Append(character);
        _hasArgument = true;
    }

    private void Flush()
    {
        if (!_hasArgument)
        {
            return;
        }

        _arguments.Add(_current.ToString());
        _current.Clear();
        _hasArgument = false;
    }
}
