namespace Datapoint.Terminal.Agent.Workspaces;

// Joins two versions of a file that both started from a common one, when they changed different lines: each version's changes
// are runs of lines replaced in the common one, and two runs that touch or overlap count as a conflict, so nothing is guessed.
public static class LineMerge
{
    public static string? TryMerge(string common, string ours, string theirs)
    {
        ArgumentNullException.ThrowIfNull(common);
        ArgumentNullException.ThrowIfNull(ours);
        ArgumentNullException.ThrowIfNull(theirs);
        var lines = LineDiff.Lines(common);
        var mine = Edits(lines, LineDiff.Lines(ours));
        var other = Edits(lines, LineDiff.Lines(theirs));
        var both = mine.Concat(other.Where(edit => !mine.Contains(edit))).OrderBy(edit => edit.Start).ToList();
        for (var index = 1; index < both.Count; index++)
        {
            if (both[index].Start <= both[index - 1].End)
            {
                return null;
            }
        }

        return Apply(lines, both);
    }

    private static string Apply(string[] lines, List<Edit> edits)
    {
        var result = new List<string>();
        var at = 0;
        foreach (var edit in edits)
        {
            result.AddRange(lines[at..edit.Start]);
            result.AddRange(edit.Lines);
            at = edit.End;
        }

        result.AddRange(lines[at..]);
        return string.Join('\n', result) + "\n";
    }

    private static List<Edit> Edits(string[] common, string[] changed)
    {
        var edits = new List<Edit>();
        var at = 0;
        Edit? open = null;
        foreach (var (kind, text) in LineDiff.Script(common, changed))
        {
            if (kind == ' ')
            {
                Close();
                at++;
                continue;
            }

            open ??= new Edit(at, at, []);
            if (kind == '-')
            {
                at++;
                open = open with { End = at };
            }
            else
            {
                open.Lines.Add(text);
            }
        }

        Close();
        return edits;

        void Close()
        {
            if (open is null)
            {
                return;
            }

            edits.Add(open);
            open = null;
        }
    }

    // Lines Start to End of the common version replaced by Lines; Start equals End for lines only added.
    private sealed record Edit(int Start, int End, List<string> Lines)
    {
        public bool Equals(Edit? other) => other is not null && Start == other.Start && End == other.End && Lines.SequenceEqual(other.Lines, StringComparer.Ordinal);

        public override int GetHashCode() => HashCode.Combine(Start, End, Lines.Count);
    }
}
