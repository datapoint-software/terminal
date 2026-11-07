using System.Text;

namespace Datapoint.Terminal.Agent.Files;

// A unified diff of two texts by line, as git and patch show it, for the user to approve an edit. Myers' algorithm, which is
// fast when the texts differ little, as an edit's do.
public static class TextDiff
{
    private const int Context = 3;

    public static string Unified(string path, string before, string after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        var a = Lines(before);
        var b = Lines(after);
        var operations = Operations(a, b);
        var text = new StringBuilder().Append("--- ").Append(path).Append('\n').Append("+++ ").Append(path).Append('\n');
        foreach (var hunk in Hunks(operations))
        {
            AppendHunk(text, operations, hunk.Start, hunk.End);
        }

        return text.ToString();
    }

    private static string[] Lines(string text) => text.Length == 0 ? [] : text.EndsWith('\n') ? text[..^1].Split('\n') : text.Split('\n');

    private enum Kind
    {
        Same,
        Removed,
        Added,
    }

    private readonly record struct Operation(Kind Kind, int Before, int After, string Text);

    private static List<Operation> Operations(string[] a, string[] b)
    {
        var trace = Trace(a, b);
        var result = new List<Operation>();
        int x = a.Length, y = b.Length;
        var offset = a.Length + b.Length;
        for (var d = trace.Count - 1; d >= 0 && (x > 0 || y > 0); d--)
        {
            var v = trace[d];
            var k = x - y;
            var previous = IsDown(v, offset, k, d) ? k + 1 : k - 1;
            var previousX = v[offset + previous];
            var previousY = previousX - previous;
            while (x > previousX && y > previousY)
            {
                result.Add(new Operation(Kind.Same, --x, --y, a[x]));
            }

            if (d > 0)
            {
                result.Add(x == previousX ? new Operation(Kind.Added, x, --y, b[y]) : new Operation(Kind.Removed, --x, y, a[x]));
            }
        }

        result.Reverse();
        return result;
    }

    // The furthest x on each diagonal before each number of changes, kept so the path can be walked back.
    private static List<int[]> Trace(string[] a, string[] b)
    {
        var max = a.Length + b.Length;
        var v = new int[(2 * max) + 2];
        var trace = new List<int[]>();
        for (var d = 0; d <= max; d++)
        {
            trace.Add((int[])v.Clone());
            for (var k = -d; k <= d; k += 2)
            {
                var x = Slide(a, b, IsDown(v, max, k, d) ? v[max + k + 1] : v[max + k - 1] + 1, k);
                v[max + k] = x;
                if (x >= a.Length && x - k >= b.Length)
                {
                    return trace;
                }
            }
        }

        return trace;
    }

    // Whether the step onto diagonal k comes from the one above (an added line) rather than the one below (a removed line).
    private static bool IsDown(int[] v, int offset, int k, int d) => k == -d || (k != d && v[offset + k - 1] < v[offset + k + 1]);

    // Follows matching lines along diagonal k from x.
    private static int Slide(string[] a, string[] b, int x, int k)
    {
        while (x < a.Length && x - k < b.Length && string.Equals(a[x], b[x - k], StringComparison.Ordinal))
        {
            x++;
        }

        return x;
    }

    // Changes with up to twice the context between them share a hunk.
    private static IEnumerable<(int Start, int End)> Hunks(List<Operation> operations)
    {
        var changes = operations.Select((operation, index) => (operation, index)).Where(item => item.operation.Kind != Kind.Same).Select(item => item.index).ToList();
        var index = 0;
        while (index < changes.Count)
        {
            var start = Math.Max(0, changes[index] - Context);
            var end = changes[index];
            while (index + 1 < changes.Count && changes[index + 1] - end <= 2 * Context)
            {
                end = changes[++index];
            }

            index++;
            yield return (start, Math.Min(operations.Count, end + Context + 1));
        }
    }

    private static void AppendHunk(StringBuilder text, List<Operation> operations, int start, int end)
    {
        var lines = operations.GetRange(start, end - start);
        var beforeCount = lines.Count(line => line.Kind != Kind.Added);
        var afterCount = lines.Count(line => line.Kind != Kind.Removed);
        var beforeStart = beforeCount == 0 ? lines[0].Before : lines.First(line => line.Kind != Kind.Added).Before + 1;
        var afterStart = afterCount == 0 ? lines[0].After : lines.First(line => line.Kind != Kind.Removed).After + 1;
        text.Append("@@ -").Append(Range(beforeStart, beforeCount)).Append(" +").Append(Range(afterStart, afterCount)).Append(" @@\n");
        foreach (var line in lines)
        {
            text.Append(line.Kind switch { Kind.Removed => '-', Kind.Added => '+', _ => ' ' }).Append(line.Text).Append('\n');
        }
    }

    private static string Range(int start, int count) =>
        count == 1 ? start.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{start},{count}");
}
