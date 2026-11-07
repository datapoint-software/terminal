using System.Text;

namespace Datapoint.Terminal.Agent.Workspaces;

// A line diff for showing people what changed, with a few lines of context around each change: "- " removed, "+ " added.
public static class LineDiff
{
    private const int Context = 3;
    private const long MaxCells = 4_000_000;

    public static string Describe(string before, string after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        var old = Lines(before);
        var now = Lines(after);
        var lines = Compare(old, now);
        return Format(lines);
    }

    private static string[] Lines(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');

    private static List<(char Kind, string Text)> Compare(string[] old, string[] now)
    {
        // Lines shared at the start and the end are set aside, so the table below covers only the part that changed.
        var start = 0;
        while (start < old.Length && start < now.Length && string.Equals(old[start], now[start], StringComparison.Ordinal))
        {
            start++;
        }

        var end = 0;
        while (end < old.Length - start && end < now.Length - start && string.Equals(old[^(end + 1)], now[^(end + 1)], StringComparison.Ordinal))
        {
            end++;
        }

        var result = old[..start].Select(line => (' ', line)).ToList();
        result.AddRange(Middle(old[start..^end], now[start..^end]));
        result.AddRange(old[^end..].Select(line => (' ', line)));
        return result;
    }

    // Longest common subsequence; beyond a size where that gets slow, everything in between counts as replaced.
    private static List<(char Kind, string Text)> Middle(string[] old, string[] now)
    {
        if ((long)(old.Length + 1) * (now.Length + 1) > MaxCells)
        {
            return [.. old.Select(line => ('-', line)), .. now.Select(line => ('+', line))];
        }

        return Walk(old, now, Table(old, now));
    }

    private static int[,] Table(string[] old, string[] now)
    {
        var table = new int[old.Length + 1, now.Length + 1];
        for (var i = old.Length - 1; i >= 0; i--)
        {
            for (var j = now.Length - 1; j >= 0; j--)
            {
                table[i, j] = string.Equals(old[i], now[j], StringComparison.Ordinal) ? table[i + 1, j + 1] + 1 : Math.Max(table[i + 1, j], table[i, j + 1]);
            }
        }

        return table;
    }

    private static List<(char Kind, string Text)> Walk(string[] old, string[] now, int[,] table)
    {
        var result = new List<(char, string)>();
        int a = 0, b = 0;
        while (a < old.Length || b < now.Length)
        {
            if (a < old.Length && b < now.Length && string.Equals(old[a], now[b], StringComparison.Ordinal))
            {
                result.Add((' ', old[a++]));
                b++;
            }
            else if (b < now.Length && (a == old.Length || table[a, b + 1] > table[a + 1, b]))
            {
                result.Add(('+', now[b++]));
            }
            else
            {
                result.Add(('-', old[a++]));
            }
        }

        return result;
    }

    private static string Format(List<(char Kind, string Text)> lines)
    {
        var changed = lines.Select((line, index) => (line, index)).Where(item => item.line.Kind != ' ').Select(item => item.index).ToList();
        var text = new StringBuilder();
        var last = -1;
        for (var index = 0; index < lines.Count; index++)
        {
            if (!changed.Any(position => Math.Abs(position - index) <= Context))
            {
                continue;
            }

            if (last >= 0 && index > last + 1)
            {
                text.AppendLine("…");
            }

            text.Append(lines[index].Kind).Append(' ').AppendLine(lines[index].Text);
            last = index;
        }

        return text.ToString().TrimEnd('\n', '\r');
    }
}
