namespace Datapoint.Terminal.Chat;

// Product decision 144: part of a reply goes into the user's message as a Markdown quote, ordinary text the user sees, can
// edit and sends as their own, so what reaches the provider stays in view.
public static class MessageQuote
{
    // The draft with the quote put at the caret, on lines of its own, and where the caret goes next: after the quote, ready
    // for the user to write about it.
    public static (string Draft, int Caret) Insert(string? draft, int caret, string selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        draft ??= string.Empty;
        caret = Math.Clamp(caret, 0, draft.Length);
        var lines = selection.ReplaceLineEndings("\n").Trim('\n').Split('\n').Select(line => line.TrimEnd());
        var quote = string.Join('\n', lines.Select(line => line.Length == 0 ? ">" : "> " + line));
        var before = draft[..caret];
        var after = draft[caret..];
        var lead = before.Length == 0 || before.EndsWith("\n\n", StringComparison.Ordinal) ? string.Empty : before.EndsWith('\n') ? "\n" : "\n\n";
        var inserted = lead + quote + "\n\n";
        return (before + inserted + after.TrimStart('\n'), before.Length + inserted.Length);
    }
}
