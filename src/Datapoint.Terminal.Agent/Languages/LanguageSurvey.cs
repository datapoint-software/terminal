using System.Text;

namespace Datapoint.Terminal.Agent.Languages;

// Product decision 151: the language of each of the user's files, counted on this computer, so the model can write a new file in
// the language of those closest to it. Only the counts and the hosts' names reach the model, never the files' text.
public sealed class LanguageSurvey
{
    private const int MostNames = 30;

    private readonly Dictionary<string, string?> _hosts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string?>> _groups = new(StringComparer.Ordinal);

    public bool IsEmpty => _hosts.Count == 0 && _groups.Count == 0;

    public void AddHost(string name, string text) => _hosts[name] = TextLanguage.Detect(text);

    // A kind of file, e.g. "skills": every file of that kind counts.
    public void Add(string group, string text)
    {
        if (!_groups.TryGetValue(group, out var languages))
        {
            _groups[group] = languages = [];
        }

        languages.Add(TextLanguage.Detect(text));
    }

    // A line for all the files, one for each kind, and the hosts by language; "none yet" when there's nothing to count.
    public string Describe(Func<string, string> languageName)
    {
        ArgumentNullException.ThrowIfNull(languageName);
        if (IsEmpty)
        {
            return "- none yet\n";
        }

        var text = new StringBuilder();
        var all = _hosts.Values.Concat(_groups.Values.SelectMany(languages => languages)).ToList();
        text.Append("- all: ").Append(Counts(all, languageName)).Append('\n');
        if (_hosts.Count > 0)
        {
            text.Append("- host documentation: ").Append(Counts([.. _hosts.Values], languageName)).Append('\n');
            foreach (var group in _hosts.GroupBy(host => host.Value, StringComparer.Ordinal).OrderBy(group => group.Key ?? "~", StringComparer.Ordinal))
            {
                var names = group.Select(host => host.Key).Order(StringComparer.OrdinalIgnoreCase).ToList();
                text.Append("  - ").Append(group.Key is { } language ? languageName(language) : "unclear").Append(": ")
                    .Append(string.Join(", ", names.Take(MostNames)))
                    .Append(names.Count > MostNames ? $" and {names.Count - MostNames} more" : string.Empty).Append('\n');
            }
        }

        foreach (var (group, languages) in _groups.OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            text.Append("- ").Append(group).Append(": ").Append(Counts(languages, languageName)).Append('\n');
        }

        return text.ToString();
    }

    private static string Counts(List<string?> languages, Func<string, string> languageName) =>
        string.Join(", ", languages
            .GroupBy(language => language, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key ?? "~", StringComparer.Ordinal)
            .Select(group => $"{(group.Key is { } language ? languageName(language) : "unclear")} {group.Count()}"));
}
