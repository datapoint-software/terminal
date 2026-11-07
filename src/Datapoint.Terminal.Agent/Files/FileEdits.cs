namespace Datapoint.Terminal.Agent.Files;

// Replacements of exact text, each found once, applied in order. A file with Windows line endings takes edits written with
// plain ones, since the model sees no difference.
public static class FileEdits
{
    public static string Apply(string text, IReadOnlyList<FileEdit> edits, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(edits);
        problem = null;
        var crlf = text.Contains("\r\n", StringComparison.Ordinal);
        for (var index = 0; index < edits.Count; index++)
        {
            var (old, replacement) = (Lines(edits[index].OldText, crlf), Lines(edits[index].NewText, crlf));
            problem = Problem(text, old, index + 1, edits.Count);
            if (problem is not null)
            {
                return text;
            }

            var at = text.IndexOf(old, StringComparison.Ordinal);
            text = string.Concat(text.AsSpan(0, at), replacement, text.AsSpan(at + old.Length));
        }

        return text;
    }

    private static string Lines(string text, bool crlf) =>
        crlf && !text.Contains('\r', StringComparison.Ordinal) ? text.Replace("\n", "\r\n", StringComparison.Ordinal) : text;

    private static string? Problem(string text, string old, int number, int count)
    {
        var which = count == 1 ? "the text to replace" : $"edit {number}'s text to replace";
        if (old.Length == 0)
        {
            return $"{Capitalize(which)} is empty. To add text, include the line it goes after.";
        }

        var first = text.IndexOf(old, StringComparison.Ordinal);
        if (first < 0)
        {
            return $"{Capitalize(which)} isn't in the file. Read it again with read_file and copy the text exactly, spaces and tabs included.";
        }

        return text.IndexOf(old, first + 1, StringComparison.Ordinal) >= 0
            ? $"{Capitalize(which)} appears more than once. Include more of the lines around it."
            : null;
    }

    private static string Capitalize(string text) => char.ToUpperInvariant(text[0]) + text[1..];
}
