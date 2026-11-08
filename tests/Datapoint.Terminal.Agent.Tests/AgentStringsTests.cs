using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Datapoint.Terminal.Agent.Resources;

namespace Datapoint.Terminal.Agent.Tests;

// Product decision 150.
public sealed partial class AgentStringsTests
{
    [Fact]
    public void Translates_every_message_into_portuguese_with_its_placeholders()
    {
        var english = Values("AgentStrings.resx");
        var portuguese = Values("AgentStrings.pt-PT.resx");

        var wrong = english.Where(pair => !portuguese.TryGetValue(pair.Key, out var text) || !Placeholders(text).SequenceEqual(Placeholders(pair.Value), StringComparer.Ordinal)).Select(pair => pair.Key).ToList();

        Assert.Empty(wrong);
    }

    [Fact]
    public void Reads_a_message_in_the_conversations_language()
    {
        var english = AgentStrings.Get("NoticeInterrupted", CultureInfo.GetCultureInfo("en"));
        var portuguese = AgentStrings.Get("NoticeInterrupted", CultureInfo.GetCultureInfo("pt-PT"));

        Assert.NotEqual(english, portuguese, StringComparer.Ordinal);
    }

    private static Dictionary<string, string> Values(string file)
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "NOTICE")))
        {
            folder = folder.Parent;
        }

        var path = Path.Combine(folder!.FullName, "src", "Datapoint.Terminal.Agent", "Resources", file);
        return XDocument.Load(path).Root!.Elements("data").ToDictionary(data => (string)data.Attribute("name")!, data => (string)data.Element("value")!, StringComparer.Ordinal);
    }

    private static List<string> Placeholders(string text) => [.. Placeholder().Matches(text).Select(match => match.Value).Order(StringComparer.Ordinal)];

    [GeneratedRegex(@"\{\d+[^}]*\}", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Placeholder();
}
