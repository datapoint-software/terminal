using System.Globalization;
using System.Text.RegularExpressions;

namespace Datapoint.Terminal.Editor;

// The marker at the start of a list item, so Enter can start the next item the same way.
public static partial class ListMarker
{
    // The marker as written, e.g. "- ", "  * [x] " or "3. ", or null when the text doesn't start a list item.
    public static string? Of(string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var match = Marker().Match(body);
        return match.Success ? match.Value : null;
    }

    // The marker for the item after this one: the next number, and an open box for a task.
    public static string Next(string marker)
    {
        ArgumentNullException.ThrowIfNull(marker);
        var match = Marker().Match(marker);
        if (!match.Success)
        {
            return string.Empty;
        }

        var indent = match.Groups["indent"].Value;
        var task = match.Groups["task"].Success ? "[ ] " : string.Empty;
        if (match.Groups["number"].Success && int.TryParse(match.Groups["number"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return string.Create(CultureInfo.InvariantCulture, $"{indent}{number + 1}{match.Groups["delimiter"].Value} {task}");
        }

        return $"{indent}{match.Groups["bullet"].Value} {task}";
    }

    [GeneratedRegex(@"^(?<indent>[ ]{0,3})(?:(?<bullet>[-*+])|(?<number>\d{1,9})(?<delimiter>[.)]))[ ]+(?<task>\[[ xX]\][ ]+)?", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Marker();
}
