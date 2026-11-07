using System.Globalization;
using System.Text;

namespace Datapoint.Terminal.Shell.Areas;

// Moves areas around a layout and saves it as text, e.g. "row(640;agent,column(280;terminals,documents))".
public static class AreaLayout
{
    private const double AgentMinimumWidth = 320;
    private const double AreaMinimumWidth = 200;
    private const double AreaMinimumHeight = 120;

    public static AreaNode Default() => FromSide(TerminalSide.Right);

    // The layout from before areas could be moved: the terminals on one side of the chat, documents below them or, with the
    // terminals at the bottom, to their right.
    public static AreaNode FromSide(TerminalSide side)
    {
        var agent = new AreaLeaf(AreaKind.Agent);
        var terminals = new AreaLeaf(AreaKind.Terminals);
        var documents = new AreaLeaf(AreaKind.Documents);
        return side switch
        {
            TerminalSide.Left => new AreaSplit(isRow: true, new AreaSplit(isRow: false, terminals, documents, 280), agent, 640),
            TerminalSide.Bottom => new AreaSplit(isRow: false, agent, new AreaSplit(isRow: true, terminals, documents, 420), 300),
            _ => new AreaSplit(isRow: true, agent, new AreaSplit(isRow: false, terminals, documents, 280), 640),
        };
    }

    // The area taken out and put on one edge of another area, or of the whole window when there's no target.
    public static AreaNode Move(AreaNode root, AreaKind moved, AreaKind? target, AreaEdge edge)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (moved == target || Without(root, moved) is not { } rest)
        {
            return root;
        }

        var leaf = new AreaLeaf(moved);
        return target is { } kind ? Insert(rest, leaf, kind, edge) : Beside(rest, leaf, edge);
    }

    // The smallest width (or height) the layout can take with the shown areas each keeping a usable size.
    public static double MinimumLength(AreaNode node, bool isRow, Func<AreaKind, bool> isShown)
    {
        ArgumentNullException.ThrowIfNull(isShown);
        return node switch
        {
            AreaLeaf leaf when !isShown(leaf.Kind) => 0,
            AreaLeaf leaf => !isRow ? AreaMinimumHeight : leaf.Kind == AreaKind.Agent ? AgentMinimumWidth : AreaMinimumWidth,
            AreaSplit split when split.IsRow == isRow => SumOfShown(split, isRow, isShown),
            AreaSplit split => Math.Max(MinimumLength(split.First, isRow, isShown), MinimumLength(split.Second, isRow, isShown)),
            _ => 0,
        };
    }

    // Each split's fixed part brought within what the space it's given allows, so a moved area doesn't push another out of
    // sight. Changes the sizes in place.
    public static void Fit(AreaNode node, double width, double height, Func<AreaKind, bool> isShown)
    {
        ArgumentNullException.ThrowIfNull(isShown);
        if (node is not AreaSplit split)
        {
            return;
        }

        var length = split.IsRow ? width : height;
        var other = split.FixesFirst ? split.Second : split.First;
        var fixedMinimum = MinimumLength(split.Fixed, split.IsRow, isShown);
        var otherMinimum = MinimumLength(other, split.IsRow, isShown);
        if (fixedMinimum > 0 && otherMinimum > 0)
        {
            split.Size = Math.Clamp(split.Size, fixedMinimum, Math.Max(fixedMinimum, length - otherMinimum - 1));
        }

        var fixedLength = otherMinimum > 0 ? split.Size : length;
        var otherLength = fixedMinimum > 0 ? Math.Max(0, length - split.Size - 1) : length;
        FitPart(split, split.Fixed, fixedLength, width, height, isShown);
        FitPart(split, other, otherLength, width, height, isShown);
    }

    private static void FitPart(AreaSplit split, AreaNode part, double length, double width, double height, Func<AreaKind, bool> isShown) =>
        Fit(part, split.IsRow ? length : width, split.IsRow ? height : length, isShown);

    public static string Format(AreaNode node) => node switch
    {
        AreaLeaf leaf => leaf.Kind.ToString().ToLowerInvariant(),
        AreaSplit split => string.Create(CultureInfo.InvariantCulture, $"{(split.IsRow ? "row" : "column")}({Math.Round(split.Size)};{Format(split.First)},{Format(split.Second)})"),
        _ => string.Empty,
    };

    // Null for text that isn't a layout of all three areas, each once, e.g. from a newer version or edited by hand.
    public static AreaNode? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var reader = new Reader(text.Replace(" ", string.Empty, StringComparison.Ordinal));
        var node = reader.Node();
        return node is not null && reader.IsAtEnd && Enum.GetValues<AreaKind>().All(kind => Count(node, kind) == 1) ? node : null;
    }

    private static double SumOfShown(AreaSplit split, bool isRow, Func<AreaKind, bool> isShown)
    {
        var first = MinimumLength(split.First, isRow, isShown);
        var second = MinimumLength(split.Second, isRow, isShown);
        return first + second + (first > 0 && second > 0 ? 1 : 0);
    }

    private static int Count(AreaNode node, AreaKind kind) => node switch
    {
        AreaLeaf leaf => leaf.Kind == kind ? 1 : 0,
        AreaSplit split => Count(split.First, kind) + Count(split.Second, kind),
        _ => 0,
    };

    private static AreaNode? Without(AreaNode node, AreaKind kind)
    {
        if (node is not AreaSplit split)
        {
            return node is AreaLeaf leaf && leaf.Kind == kind ? null : node;
        }

        var first = Without(split.First, kind);
        var second = Without(split.Second, kind);
        if (first is null || second is null)
        {
            return first ?? second;
        }

        return ReferenceEquals(first, split.First) && ReferenceEquals(second, split.Second) ? split : new AreaSplit(split.IsRow, first, second, split.Size);
    }

    private static AreaNode Insert(AreaNode node, AreaLeaf moved, AreaKind target, AreaEdge edge) => node switch
    {
        AreaLeaf leaf when leaf.Kind == target => Beside(leaf, moved, edge),
        AreaSplit split when split.First.Contains(target) => new AreaSplit(split.IsRow, Insert(split.First, moved, target, edge), split.Second, split.Size),
        AreaSplit split => new AreaSplit(split.IsRow, split.First, Insert(split.Second, moved, target, edge), split.Size),
        _ => node,
    };

    // The moved area on one edge of the node, at the size such an area usually has.
    private static AreaSplit Beside(AreaNode node, AreaLeaf moved, AreaEdge edge)
    {
        var isRow = edge is AreaEdge.Left or AreaEdge.Right;
        var split = edge is AreaEdge.Left or AreaEdge.Top ? new AreaSplit(isRow, moved, node, 0) : new AreaSplit(isRow, node, moved, 0);
        split.Size = split.Fixed is AreaLeaf { Kind: AreaKind.Terminals } ? (isRow ? 640 : 300) : (isRow ? 420 : 280);
        return split;
    }

    private sealed class Reader(string text)
    {
        private int _at;

        public bool IsAtEnd => _at == text.Length;

        public AreaNode? Node()
        {
            var name = Word();
            if (string.Equals(name, "row", StringComparison.Ordinal) || string.Equals(name, "column", StringComparison.Ordinal))
            {
                return Split(string.Equals(name, "row", StringComparison.Ordinal));
            }

            return Enum.TryParse<AreaKind>(name, ignoreCase: true, out var kind) && Enum.IsDefined(kind) ? new AreaLeaf(kind) : null;
        }

        private AreaSplit? Split(bool isRow)
        {
            if (!Take('(') || !double.TryParse(Until(';'), NumberStyles.Float, CultureInfo.InvariantCulture, out var size) || !Take(';')
                || size is < 0 or > 100_000 || Node() is not { } first || !Take(',') || Node() is not { } second || !Take(')'))
            {
                return null;
            }

            return new AreaSplit(isRow, first, second, size);
        }

        private string Word()
        {
            var start = _at;
            while (_at < text.Length && char.IsAsciiLetter(text[_at]))
            {
                _at++;
            }

            return text[start.._at];
        }

        private string Until(char end)
        {
            var start = _at;
            while (_at < text.Length && text[_at] != end)
            {
                _at++;
            }

            return text[start.._at];
        }

        private bool Take(char expected)
        {
            if (_at >= text.Length || text[_at] != expected)
            {
                return false;
            }

            _at++;
            return true;
        }
    }
}
