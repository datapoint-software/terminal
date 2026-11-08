using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Datapoint.Terminal.Tests.Resources;

// Product decision 150. {res:Tr Key} names its key in a string, which the compiler can't check, so this does.
public sealed partial class ResourceKeysTests
{
    [Fact]
    public void Finds_every_key_a_view_names()
    {
        var keys = Keys("Strings.resx");

        var missing = Views().SelectMany(view => TrKey().Matches(File.ReadAllText(view)).Select(match => $"{Path.GetFileName(view)}: {match.Groups["key"].Value}"))
            .Where(use => !keys.Contains(use[(use.IndexOf(": ", StringComparison.Ordinal) + 2)..]))
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void Leaves_no_view_text_that_reads_the_language_only_once()
    {
        var once = Views().Where(view => File.ReadAllText(view).Contains("x:Static res:Strings.", StringComparison.Ordinal)).Select(Path.GetFileName).ToList();

        Assert.Empty(once);
    }

    [Fact]
    public void Translates_every_string_into_portuguese()
    {
        // A language's own name is the same in every language.
        var untranslated = Keys("Strings.resx").Except(Keys("Strings.pt-PT.resx"), StringComparer.Ordinal).Except(["LanguageEnglish", "LanguagePortuguese"], StringComparer.Ordinal).ToList();

        Assert.Empty(untranslated);
    }

    [Fact]
    public void Keeps_each_strings_placeholders_and_access_key_in_portuguese()
    {
        var english = Values("Strings.resx");

        var different = Values("Strings.pt-PT.resx")
            .Where(pair => !Placeholders(pair.Value).SequenceEqual(Placeholders(english[pair.Key]), StringComparer.Ordinal) || pair.Value.Count(c => c == '_') != english[pair.Key].Count(c => c == '_'))
            .Select(pair => pair.Key)
            .ToList();

        Assert.Empty(different);
    }

    private static Dictionary<string, string> Values(string file) =>
        XDocument.Load(Path.Combine(Repository(), "src", "Datapoint.Terminal", "Resources", file)).Root!.Elements("data")
            .ToDictionary(data => (string)data.Attribute("name")!, data => (string)data.Element("value")!, StringComparer.Ordinal);

    private static List<string> Placeholders(string text) => [.. Placeholder().Matches(text).Select(match => match.Value).Order(StringComparer.Ordinal)];

    internal static HashSet<string> Keys(string file) =>
        [.. XDocument.Load(Path.Combine(Repository(), "src", "Datapoint.Terminal", "Resources", file)).Root!.Elements("data").Select(data => (string)data.Attribute("name")!)];

    internal static string Repository()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "NOTICE")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new DirectoryNotFoundException("The repository's root wasn't found.");
    }

    private static IEnumerable<string> Views() => Directory.EnumerateFiles(Path.Combine(Repository(), "src"), "*.axaml", SearchOption.AllDirectories);

    [GeneratedRegex(@"\{\d+[^}]*\}", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"\{res:Tr (?<key>\w+)", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TrKey();
}
