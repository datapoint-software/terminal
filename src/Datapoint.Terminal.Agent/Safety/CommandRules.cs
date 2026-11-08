using System.Collections.Frozen;
using System.Text.Json;

namespace Datapoint.Terminal.Agent.Safety;

// The read-only commands shipped with the application, read from ReadOnlyCommands.jsonc so the list can grow and be reviewed
// without touching the code that applies it.
internal sealed class CommandRules
{
    public const int MaxWords = 3;

    private const string ResourceName = "Datapoint.Terminal.Agent.Safety.ReadOnlyCommands.jsonc";

    private static readonly JsonDocumentOptions s_options = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    private readonly FrozenDictionary<string, CommandRule> _rules;
    private readonly FrozenDictionary<string, CommandRule> _windowsRules;
    private readonly FrozenDictionary<string, string[]> _programWrites;

    private CommandRules(IReadOnlyList<string> writingOptions, IReadOnlyList<CommandRule> rules)
    {
        WritingOptions = writingOptions;
        _rules = rules.Where(rule => !rule.IsWindows).ToFrozenDictionary(rule => rule.Command, StringComparer.Ordinal);
        _windowsRules = rules.Where(rule => rule.IsWindows).ToFrozenDictionary(rule => rule.Command, StringComparer.OrdinalIgnoreCase);
        Commands = [.. rules.Select(rule => rule.Command)];
        _programWrites = rules.Where(rule => !rule.IsWindows)
            .GroupBy(rule => rule.Command.Split(' ')[0], StringComparer.Ordinal)
            .ToFrozenDictionary(group => group.Key, group => group.SelectMany(rule => rule.Writes).Distinct(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
    }

    public static CommandRules BuiltIn { get; } = LoadBuiltIn();

    public IReadOnlyList<string> WritingOptions { get; }

    public IReadOnlyList<string> Commands { get; }

    public CommandRule? Find(string command) =>
        _rules.GetValueOrDefault(command) ?? _windowsRules.GetValueOrDefault(command);

    // Every writing option listed for the program, for a command the user taught: "git notes add" stays a change with -f
    // whichever git command the option was listed for.
    public IReadOnlyList<string> WritesFor(string program) =>
        [.. WritingOptions.Concat(_programWrites.GetValueOrDefault(program, [])).Distinct(StringComparer.Ordinal)];

    public static CommandRules Parse(string json)
    {
        using var document = JsonDocument.Parse(json, s_options);
        var root = document.RootElement;
        var global = Strings(root, "writingOptions");
        var rules = new List<CommandRule>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var seenWindows = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in root.GetProperty("groups").EnumerateArray())
        {
            foreach (var rule in Group(group, global))
            {
                if (!(rule.IsWindows ? seenWindows : seen).Add(rule.Command))
                {
                    throw new InvalidDataException($"\"{rule.Command}\" is listed twice.");
                }

                rules.Add(rule);
            }
        }

        return new CommandRules(global, rules);
    }

    private static IEnumerable<CommandRule> Group(JsonElement group, IReadOnlyList<string> global)
    {
        var allows = Strings(group, "allows");
        var isWindows = Flag(group, "windows");
        var writes = (isWindows ? [] : global.Except(allows, StringComparer.Ordinal)).Concat(Strings(group, "writes")).ToArray();
        var isExact = Flag(group, "exact");
        foreach (var command in Strings(group, "commands"))
        {
            var words = command.Split(' ');
            if (words.Length > MaxWords || words.Any(word => word.Length == 0))
            {
                throw new InvalidDataException($"\"{command}\" isn't one to {MaxWords} words separated by single spaces.");
            }

            yield return new CommandRule(command, writes, isExact, isWindows);
        }
    }

    private static string[] Strings(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) ? [.. value.EnumerateArray().Select(item => item.GetString() ?? string.Empty)] : [];

    private static bool Flag(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.GetBoolean();

    private static CommandRules LoadBuiltIn()
    {
        using var stream = typeof(CommandRules).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(ResourceName + " isn't embedded.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }
}
