using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Datapoint.Terminal.Agent.Skills;

// SKILL.md (product decision 103), a valid Agent Skills file (agentskills.io/specification): a YAML header with name and
// description, then the steps in Markdown. Where the skill runs and how it rolls out go in the header's metadata map, the
// format's place for a client's own fields, as strings under keys starting "terminal.".
public static partial class SkillDocument
{
    public const string FileName = "SKILL.md";
    public const int MaxBytes = 32 * 1024;
    public const int MaxNameLength = 64;
    public const int MaxDescriptionLength = 1024;

    public const string LabelsKey = "terminal.labels";
    public const string OrderKey = "terminal.rollout";
    public const string BatchSizeKey = "terminal.batch-size";
    public const string MaxHostsKey = "terminal.max-hosts";
    public const string HealthCheckKey = "terminal.health-check";
    public const string HealthyForKey = "terminal.healthy-for";
    public const string TimeoutKey = "terminal.timeout";
    public const string OnFailureKey = "terminal.on-failure";

    private const string Fence = "---";

    // Lowercase letters, digits and single hyphens, as the format requires of a name, which is also the folder's.
    public static bool IsValidName(string name) => name.Length is > 0 and <= MaxNameLength && NamePattern().IsMatch(name);

    public static string NameFor(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        var slug = new StringBuilder();
        foreach (var character in title.Trim().ToLowerInvariant())
        {
            slug.Append(char.IsAsciiLetterOrDigit(character) ? character : '-');
        }

        var text = Hyphens().Replace(slug.ToString(), "-").Trim('-');
        text = text.Length > MaxNameLength ? text[..MaxNameLength].TrimEnd('-') : text;
        return text.Length == 0 ? "skill" : text;
    }

    public static SkillHeader ReadHeader(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        if (lines.Length == 0 || !string.Equals(lines[0].TrimEnd(), Fence, StringComparison.Ordinal))
        {
            return new SkillHeader([], [], text);
        }

        var end = Array.FindIndex(lines, 1, line => string.Equals(line.TrimEnd(), Fence, StringComparison.Ordinal));
        if (end < 0)
        {
            return new SkillHeader([], [], text);
        }

        var fields = new List<(string, string)>();
        var metadata = new List<(string, string)>();
        ReadFields(lines[1..end], fields, metadata);
        return new SkillHeader(fields, metadata, string.Join('\n', lines[(end + 1)..]).TrimStart('\n'));
    }

    // Top-level fields, and the indented lines under "metadata:" as its map.
    private static void ReadFields(string[] lines, List<(string, string)> fields, List<(string, string)> metadata)
    {
        var inMetadata = false;
        foreach (var line in lines)
        {
            if (line.Length == 0 || line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            var isIndented = char.IsWhiteSpace(line[0]);
            if (!isIndented)
            {
                inMetadata = false;
            }

            if (KeyValue(line.Trim()) is not var (key, value))
            {
                continue;
            }

            if (isIndented && inMetadata)
            {
                metadata.Add((key, value));
            }
            else if (!isIndented)
            {
                inMetadata = string.Equals(key, "metadata", StringComparison.Ordinal) && value.Length == 0;
                fields.Add((key, value));
            }
        }
    }

    public static Skill Read(string text, string path)
    {
        var header = ReadHeader(text);
        var folder = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path)) ?? string.Empty;
        var name = header.Field("name") is { Length: > 0 } given ? given : folder;
        var title = Heading(header.Body) ?? Words(name);
        return new Skill(name, title, header.Field("description") ?? string.Empty, Labels(header.MetadataValue(LabelsKey)), RolloutFrom(header), header.Body, path);
    }

    public static string Create(string name, string description, IReadOnlyList<string> labels, SkillRollout rollout, string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return Fence + "\n" + "name: " + name + "\n" + "description: " + Quote(OneLine(description)) + "\n"
            + MetadataBlock(Values(labels, rollout), []) + Fence + "\n\n" + body.Trim() + "\n";
    }

    // Rewrites only this application's metadata keys, keeping every other line of the file as it was.
    public static string WithSettings(string text, IReadOnlyList<string> labels, SkillRollout rollout, string? description = null)
    {
        var header = ReadHeader(text);
        var others = header.Metadata.Where(item => !item.Key.StartsWith("terminal.", StringComparison.Ordinal)).ToList();
        var fields = header.Fields.Where(field => !string.Equals(field.Key, "metadata", StringComparison.Ordinal))
            .Select(field => field.Key + ": " + (description is not null && string.Equals(field.Key, "description", StringComparison.Ordinal) ? Quote(OneLine(description)) : RawValue(text, field.Key, field.Value)));
        return Fence + "\n" + string.Concat(fields.Select(line => line + "\n")) + MetadataBlock(Values(labels, rollout), others) + Fence + "\n\n" + header.Body.TrimStart('\n');
    }

    public static IReadOnlyList<string> Labels(string? text) =>
        text is null ? [] : [.. text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase)];

    // "90s", "5m", "1h", or minutes when there's no unit.
    public static TimeSpan? Duration(string? text)
    {
        if (text is null || DurationPattern().Match(text.Trim()) is not { Success: true } match)
        {
            return null;
        }

        var number = double.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture);
        var span = match.Groups["unit"].Value switch
        {
            "s" => TimeSpan.FromSeconds(number),
            "h" => TimeSpan.FromHours(number),
            _ => TimeSpan.FromMinutes(number),
        };
        return span <= SkillRollout.MaxDuration ? span : null;
    }

    public static string FormatDuration(TimeSpan span) =>
        span.TotalHours >= 1 && span.TotalHours == Math.Floor(span.TotalHours) ? span.TotalHours.ToString(CultureInfo.InvariantCulture) + "h"
        : span.TotalMinutes >= 1 && span.TotalMinutes == Math.Floor(span.TotalMinutes) ? span.TotalMinutes.ToString(CultureInfo.InvariantCulture) + "m"
        : Math.Round(span.TotalSeconds).ToString(CultureInfo.InvariantCulture) + "s";

    public static string? Heading(string body) =>
        body.Split('\n').Select(line => line.TrimEnd('\r')).FirstOrDefault(line => line.StartsWith("# ", StringComparison.Ordinal))?[2..].Trim() is { Length: > 0 and <= 80 } heading
            ? heading
            : null;

    private static string Words(string name)
    {
        var words = name.Replace('-', ' ').Trim();
        return words.Length == 0 ? name : char.ToUpperInvariant(words[0]) + words[1..];
    }

    private static SkillRollout RolloutFrom(SkillHeader header)
    {
        var defaults = new SkillRollout();
        return new SkillRollout
        {
            Order = header.MetadataValue(OrderKey) switch
            {
                "batches" => RolloutOrder.Batches,
                "all-at-once" => RolloutOrder.AllAtOnce,
                _ => RolloutOrder.OneByOne,
            },
            BatchSize = int.TryParse(header.MetadataValue(BatchSizeKey), NumberStyles.None, CultureInfo.InvariantCulture, out var size) && size is > 0 and <= 1000 ? size : defaults.BatchSize,
            MaxHosts = int.TryParse(header.MetadataValue(MaxHostsKey), NumberStyles.None, CultureInfo.InvariantCulture, out var most) && most is > 0 and <= 1000 ? most : defaults.MaxHosts,
            HealthCheck = header.MetadataValue(HealthCheckKey) is { Length: > 0 } check ? check : null,
            HealthyFor = Duration(header.MetadataValue(HealthyForKey)) ?? defaults.HealthyFor,
            Timeout = Duration(header.MetadataValue(TimeoutKey)) ?? defaults.Timeout,
            OnFailure = header.MetadataValue(OnFailureKey) switch
            {
                "continue" => RolloutFailure.Continue,
                "ask" => RolloutFailure.Ask,
                _ => RolloutFailure.Stop,
            },
        };
    }

    private static List<(string Key, string Value)> Values(IReadOnlyList<string> labels, SkillRollout rollout)
    {
        var values = new List<(string, string)>();
        if (labels.Count > 0)
        {
            values.Add((LabelsKey, string.Join(", ", labels)));
        }

        values.Add((OrderKey, rollout.Order switch
        {
            RolloutOrder.Batches => "batches",
            RolloutOrder.AllAtOnce => "all-at-once",
            _ => "one-by-one",
        }));
        if (rollout.Order == RolloutOrder.Batches)
        {
            values.Add((BatchSizeKey, rollout.BatchSize.ToString(CultureInfo.InvariantCulture)));
        }

        if (rollout.Order == RolloutOrder.AllAtOnce)
        {
            values.Add((MaxHostsKey, rollout.MaxHosts.ToString(CultureInfo.InvariantCulture)));
        }

        if (rollout.HealthCheck is { } check)
        {
            values.Add((HealthCheckKey, OneLine(check)));
            values.Add((HealthyForKey, FormatDuration(rollout.HealthyFor)));
        }

        values.Add((TimeoutKey, FormatDuration(rollout.Timeout)));
        values.Add((OnFailureKey, rollout.OnFailure.ToString().ToLowerInvariant()));
        return values;
    }

    private static string MetadataBlock(List<(string Key, string Value)> ours, List<(string Key, string Value)> others) =>
        others.Count + ours.Count == 0
            ? string.Empty
            : "metadata:\n" + string.Concat(others.Concat(ours).Select(item => "  " + item.Key + ": " + Quote(item.Value) + "\n"));

    // The value as it was written, quotes and all, so an untouched field isn't rewritten.
    private static string RawValue(string text, string key, string value)
    {
        var line = text.ReplaceLineEndings("\n").Split('\n').FirstOrDefault(item => item.StartsWith(key + ":", StringComparison.Ordinal));
        return line is null ? Quote(value) : line[(key.Length + 1)..].Trim();
    }

    private static (string Key, string Value)? KeyValue(string line)
    {
        var colon = line.IndexOf(':', StringComparison.Ordinal);
        if (colon <= 0)
        {
            return null;
        }

        var key = line[..colon].Trim();
        var value = line[(colon + 1)..].Trim();
        return (key, Unquote(value));
    }

    private static string Unquote(string value) =>
        value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\''))
            ? (value[0] == '"' ? value[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal).Replace("\\\\", "\\", StringComparison.Ordinal) : value[1..^1].Replace("''", "'", StringComparison.Ordinal))
            : value;

    // Always double-quoted, so a value with a colon, a # or a leading symbol stays a plain string in YAML.
    private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static string OneLine(string text) => text.ReplaceLineEndings(" ").Trim();

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex NamePattern();

    [GeneratedRegex("-{2,}", RegexOptions.None, 1000)]
    private static partial Regex Hyphens();

    [GeneratedRegex(@"^(?<number>\d{1,5}(\.\d{1,3})?)\s*(?<unit>[smh]?)$", RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex DurationPattern();
}
