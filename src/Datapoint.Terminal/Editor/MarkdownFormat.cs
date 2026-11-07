namespace Datapoint.Terminal.Editor;

// Bold, italic, code and links typed for the user around the selection, as the Markdown they'd type themselves. Applying the
// same format again takes it off, as in a word processor.
public static class MarkdownFormat
{
    public static FormatEdit Wrap(string text, int start, int end, string marker)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrEmpty(marker);
        (start, end) = Ordered(text, start, end);
        var selected = text[start..end];
        if (selected.Length >= marker.Length * 2 && selected.StartsWith(marker, StringComparison.Ordinal) && selected.EndsWith(marker, StringComparison.Ordinal))
        {
            var inner = selected[marker.Length..^marker.Length];
            return new FormatEdit(text[..start] + inner + text[end..], start, start + inner.Length);
        }

        if (start >= marker.Length && end + marker.Length <= text.Length
            && string.Equals(text[(start - marker.Length)..start], marker, StringComparison.Ordinal)
            && string.Equals(text[end..(end + marker.Length)], marker, StringComparison.Ordinal)
            && !IsLongerRun(text, start - marker.Length, end + marker.Length, marker[0]))
        {
            return new FormatEdit(text[..(start - marker.Length)] + selected + text[(end + marker.Length)..], start - marker.Length, end - marker.Length);
        }

        return new FormatEdit(text[..start] + marker + selected + marker + text[end..], start + marker.Length, end + marker.Length);
    }

    // The selection becomes the link's text, and the address is selected, ready to be typed or pasted over.
    public static FormatEdit Link(string text, int start, int end)
    {
        ArgumentNullException.ThrowIfNull(text);
        (start, end) = Ordered(text, start, end);
        const string Address = "https://";
        var label = text[start..end];
        var result = text[..start] + "[" + label + "](" + Address + ")" + text[end..];
        var addressStart = start + label.Length + 3;
        return label.Length == 0
            ? new FormatEdit(result, start + 1, start + 1)
            : new FormatEdit(result, addressStart, addressStart + Address.Length);
    }

    private static (int Start, int End) Ordered(string text, int start, int end)
    {
        var low = Math.Clamp(Math.Min(start, end), 0, text.Length);
        var high = Math.Clamp(Math.Max(start, end), 0, text.Length);
        return (low, high);
    }

    // "*" around a selection inside "**bold**" is part of the bold, not italic of its own.
    private static bool IsLongerRun(string text, int before, int after, char character) =>
        (before > 0 && text[before - 1] == character) || (after < text.Length && text[after] == character);
}
