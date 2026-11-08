using System.Text.RegularExpressions;

namespace Datapoint.Terminal.Agent.Languages;

// Product decision 151: tells English from Portuguese by their most common words, on this computer, so the model learns the
// language of the user's files without their text. Code, inline code and anything with a dot in it (domains, file names,
// versions) are left out, as they're English whatever the prose is. Words both languages use ("a", "as", "do", "no") don't count.
public static partial class TextLanguage
{
    public const string English = "en";
    public const string Portuguese = "pt";

    private const int FewestWords = 5;

    private static readonly HashSet<string> s_english = new(StringComparer.Ordinal)
    {
        "the", "and", "of", "to", "is", "in", "that", "for", "it", "with", "on", "this", "be", "are", "by", "or", "not",
        "from", "if", "when", "you", "at", "an", "which", "will", "should", "can", "after", "before", "only", "each", "its",
    };

    private static readonly HashSet<string> s_portuguese = new(StringComparer.Ordinal)
    {
        "de", "que", "não", "o", "os", "um", "uma", "para", "com", "da", "dos", "das", "em", "na", "nas", "nos", "se", "por",
        "mais", "é", "ao", "como", "mas", "ou", "também", "este", "esta", "isso", "quando", "ser", "está", "são", "pelo",
        "pela", "e", "antes", "depois", "cada", "só", "deve", "pode",
    };

    // English, Portuguese, or null when there are too few words to tell or neither has at least twice the other's.
    public static string? Detect(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var prose = Dotted().Replace(InlineCode().Replace(CodeBlock().Replace(text, " "), " "), " ");
        int english = 0, portuguese = 0;
        foreach (Match word in Word().Matches(prose))
        {
            var value = word.Value.ToLowerInvariant();
            english += s_english.Contains(value) ? 1 : 0;
            portuguese += s_portuguese.Contains(value) ? 1 : 0;
        }

        return english + portuguese < FewestWords ? null
            : english >= 2 * portuguese ? English
            : portuguese >= 2 * english ? Portuguese
            : null;
    }

    [GeneratedRegex(@"^(?<fence>```|~~~).*?^\k<fence>", RegexOptions.Multiline | RegexOptions.Singleline, 1000)]
    private static partial Regex CodeBlock();

    [GeneratedRegex(@"`[^`\n]*`", RegexOptions.None, 1000)]
    private static partial Regex InlineCode();

    [GeneratedRegex(@"\S*\w\.\w\S*", RegexOptions.None, 1000)]
    private static partial Regex Dotted();

    [GeneratedRegex(@"\p{L}+", RegexOptions.None, 1000)]
    private static partial Regex Word();
}
