using System.Globalization;
using System.Text;

namespace Datapoint.Terminal.Agent.Keys;

// Keys the agent sends to full-screen programs (product decision 21), written as text with named keys in angle brackets, as in
// Vim: "<Esc>:wq<Enter>", "<C-c>", "<Down><Down>". "<lt>" is a literal "<". Encoded as the keyboard would send them.
public static class KeyNotation
{
    public const int MaxLength = 200;

    private static readonly Dictionary<string, string> s_named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Enter"] = "\r",
        ["Return"] = "\r",
        ["CR"] = "\r",
        ["Esc"] = "\e",
        ["Escape"] = "\e",
        ["Tab"] = "\t",
        ["S-Tab"] = "\e[Z",
        ["BS"] = "\x7f",
        ["Backspace"] = "\x7f",
        ["Del"] = "\e[3~",
        ["Delete"] = "\e[3~",
        ["Insert"] = "\e[2~",
        ["PageUp"] = "\e[5~",
        ["PageDown"] = "\e[6~",
        ["Space"] = " ",
        ["lt"] = "<",
        ["F1"] = "\eOP",
        ["F2"] = "\eOQ",
        ["F3"] = "\eOR",
        ["F4"] = "\eOS",
        ["F5"] = "\e[15~",
        ["F6"] = "\e[17~",
        ["F7"] = "\e[18~",
        ["F8"] = "\e[19~",
        ["F9"] = "\e[20~",
        ["F10"] = "\e[21~",
        ["F11"] = "\e[23~",
        ["F12"] = "\e[24~",
    };

    // Cursor keys depend on the program's choice of cursor key mode (DECCKM).
    private static readonly Dictionary<string, char> s_cursor = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Up"] = 'A',
        ["Down"] = 'B',
        ["Right"] = 'C',
        ["Left"] = 'D',
        ["Home"] = 'H',
        ["End"] = 'F',
    };

    // The keys' parts in order: named keys as "<Name>", text as it is. Null with a problem when the notation isn't valid.
    public static IReadOnlyList<string>? Parse(string keys, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(keys);
        problem = keys.Length == 0 ? "no keys given." : keys.Length > MaxLength ? $"send at most {MaxLength} characters of keys at a time." : null;
        if (problem is not null)
        {
            return null;
        }

        var parts = new List<string>();
        var text = new StringBuilder();
        for (var index = 0; index < keys.Length; index++)
        {
            var close = keys[index] == '<' ? keys.IndexOf('>', index + 1) : -1;
            if (close < 0)
            {
                text.Append(keys[index]);
                continue;
            }

            var name = keys[(index + 1)..close];
            if (Encode(name, applicationCursor: false) is null)
            {
                problem = $"\"<{name}>\" isn't a key this tool knows. Use <lt> for a literal \"<\".";
                return null;
            }

            Flush(parts, text);
            parts.Add($"<{name}>");
            index = close;
        }

        Flush(parts, text);
        return parts;
    }

    public static byte[] Encode(IReadOnlyList<string> parts, bool applicationCursor)
    {
        ArgumentNullException.ThrowIfNull(parts);
        var text = new StringBuilder();
        foreach (var part in parts)
        {
            text.Append(part.Length > 2 && part[0] == '<' && part[^1] == '>' ? Encode(part[1..^1], applicationCursor) : part);
        }

        return Encoding.UTF8.GetBytes(text.ToString());
    }

    // The text parts alone, for checking what the keys would type at a shell.
    public static string Typed(IReadOnlyList<string> parts) =>
        string.Concat(parts.Select(part => part.Length > 2 && part[0] == '<' && part[^1] == '>' ? (IsEnter(part) ? "\n" : " ") : part));

    private static bool IsEnter(string part) => s_named.TryGetValue(part[1..^1], out var value) && string.Equals(value, "\r", StringComparison.Ordinal);

    private static void Flush(List<string> parts, StringBuilder text)
    {
        if (text.Length == 0)
        {
            return;
        }

        parts.Add(text.ToString());
        text.Clear();
    }

    private static string? Encode(string name, bool applicationCursor)
    {
        if (s_named.TryGetValue(name, out var value))
        {
            return value;
        }

        if (s_cursor.TryGetValue(name, out var final))
        {
            return (applicationCursor ? "\eO" : "\e[") + final;
        }

        // <C-x> is Ctrl with a letter or one of @[\]^_; <M-x> is Alt (Escape first) with any character.
        if (name.Length != 3 || name[1] != '-')
        {
            return null;
        }

        var key = name[2];
        return char.ToUpperInvariant(name[0]) switch
        {
            'C' when char.ToUpperInvariant(key) is >= '@' and <= '_' => ((char)(char.ToUpperInvariant(key) - '@')).ToString(CultureInfo.InvariantCulture),
            'M' or 'A' => "\e" + key,
            _ => null,
        };
    }
}
