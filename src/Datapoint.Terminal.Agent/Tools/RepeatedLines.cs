using System.Globalization;
using System.Text;

namespace Datapoint.Terminal.Agent.Tools;

// Product decision 137: a line repeated exactly, one after another, is sent once with how many times it repeats, so output that
// prints the same line many times (a retry loop, a warning per file, blank lines) costs one line. Only exact repeats collapse:
// lines that differ at all, even in a number, are all sent, since the difference may be what matters.
public static class RepeatedLines
{
    public const int Fewest = 3;

    public static string Collapse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = text.Split('\n');
        if (lines.Length < Fewest)
        {
            return text;
        }

        var collapsed = new StringBuilder(text.Length);
        for (var index = 0; index < lines.Length;)
        {
            var run = 1;
            while (index + run < lines.Length && string.Equals(lines[index + run], lines[index], StringComparison.Ordinal))
            {
                run++;
            }

            Append(collapsed, lines[index], index == 0);
            if (run >= Fewest)
            {
                Append(collapsed, string.Create(CultureInfo.InvariantCulture, $"[the line above repeats {run - 1} more times]"), isFirst: false);
            }
            else
            {
                for (var repeat = 1; repeat < run; repeat++)
                {
                    Append(collapsed, lines[index], isFirst: false);
                }
            }

            index += run;
        }

        return collapsed.ToString();
    }

    private static void Append(StringBuilder text, string line, bool isFirst) => (isFirst ? text : text.Append('\n')).Append(line);
}
