using System.Net;
using System.Text;

namespace Datapoint.Terminal.Agent.Safety;

// What the terminals have shown the agent in this conversation, to catch text from them leaving in an address or a network
// command: the way an injected instruction would carry data out. Compared without case and with runs of whitespace as one,
// so reformatting doesn't hide it; encoding it differently still would, which is why this only ever adds a question.
public sealed class SeenText
{
    public const int MatchLength = 24;

    private const int MaxCharacters = 256 * 1024;

    private readonly Lock _sync = new();
    private readonly StringBuilder _text = new();

    public void Add(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var normalized = Normalize(text);
        lock (_sync)
        {
            _text.Append(normalized).Append('\n');
            if (_text.Length > MaxCharacters)
            {
                _text.Remove(0, _text.Length - MaxCharacters);
            }
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _text.Clear();
        }
    }

    // Whether the text appears whole, e.g. an address the output printed.
    public bool Contains(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var normalized = Normalize(text);
        lock (_sync)
        {
            return normalized.Length > 0 && _text.ToString().Contains(normalized, StringComparison.Ordinal);
        }
    }

    // The longest stretch of the outgoing text, from its first match, that the terminals showed; null when there's none.
    public string? FindIn(string outgoing)
    {
        ArgumentNullException.ThrowIfNull(outgoing);
        var sent = Normalize(Decode(outgoing));
        if (sent.Length < MatchLength)
        {
            return null;
        }

        var windows = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i + MatchLength <= sent.Length; i++)
        {
            windows.Add(sent.Substring(i, MatchLength));
        }

        string seen;
        lock (_sync)
        {
            seen = _text.ToString();
        }

        var lookup = windows.GetAlternateLookup<ReadOnlySpan<char>>();
        for (var i = 0; i + MatchLength <= seen.Length; i++)
        {
            if (lookup.Contains(seen.AsSpan(i, MatchLength)))
            {
                return Extend(seen, i, sent);
            }
        }

        return null;
    }

    private static string Extend(string seen, int at, string sent)
    {
        var start = sent.IndexOf(seen.Substring(at, MatchLength), StringComparison.Ordinal);
        var length = MatchLength;
        while (start + length < sent.Length && at + length < seen.Length && sent[start + length] == seen[at + length] && length < 120)
        {
            length++;
        }

        return sent.Substring(start, length).Trim();
    }

    // Addresses carry text percent-encoded or with + for spaces.
    private static string Decode(string text)
    {
        try
        {
            return WebUtility.UrlDecode(text);
        }
        catch (ArgumentException)
        {
            return text;
        }
    }

    private static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (!char.IsWhiteSpace(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
            else if (builder.Length > 0 && builder[^1] != ' ')
            {
                builder.Append(' ');
            }
        }

        return builder.ToString();
    }
}
