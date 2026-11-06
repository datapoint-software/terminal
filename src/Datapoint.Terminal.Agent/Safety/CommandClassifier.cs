using System.Collections.Frozen;

namespace Datapoint.Terminal.Agent.Safety;

// Conservative: a command is read-only only when every part of it is a known reading command with no output redirection;
// anything unknown counts as a change.
public static class CommandClassifier
{
    // Programs that only read, as long as none of the options in s_writingOptions is given. Left out on purpose: env, nice,
    // time and xargs (they run other commands), ip, ifconfig and hostname (they also change settings), uniq (writes its second
    // argument), history and dmesg (can clear), and interactive programs such as less and top, which never return.
    private static readonly FrozenSet<string> s_readOnly = FrozenSet.Create(
        StringComparer.Ordinal,
        "ls", "pwd", "whoami", "id", "uname", "uptime", "date", "cat", "head", "tail", "wc", "grep", "egrep", "fgrep", "rg", "which",
        "type", "file", "stat", "df", "du", "free", "ps", "pgrep", "printenv", "echo", "printf", "lsblk", "lscpu", "lsof", "ss", "netstat",
        "dig", "nslookup", "host", "ping", "journalctl", "sort", "cut", "tr", "column", "diff", "md5sum", "sha256sum", "tree", "realpath",
        "dirname", "basename", "true", "test", "groups", "last", "w", "who", "nproc", "arch", "sw_vers", "vm_stat", "jq");

    // Options that make an otherwise reading program write, delete or run something. Matched as the whole word, or as a prefix
    // for the long options that take a value (e.g. --output=file, --vacuum-size=1G).
    private static readonly string[] s_writingOptions =
    [
        "-delete", "-exec", "-execdir", "-i", "--in-place", "-o", "--output", "--vacuum", "--rotate", "--flush", "--pre",
    ];

    // Short options that write only for one program, where they're common harmless options elsewhere (e.g. ls -s, du -s).
    private static readonly FrozenDictionary<string, string[]> s_writingOptionsByProgram = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["date"] = ["-s", "--set"],
        ["ss"] = ["-K", "--kill"],
        ["file"] = ["-C"],
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, FrozenSet<string>> s_readOnlySubcommands = new Dictionary<string, FrozenSet<string>>(StringComparer.Ordinal)
    {
        ["git"] = FrozenSet.Create(StringComparer.Ordinal, "status", "log", "diff", "show", "rev-parse", "blame", "ls-files", "describe"),
        ["systemctl"] = FrozenSet.Create(StringComparer.Ordinal, "status", "list-units", "list-unit-files", "is-active", "is-enabled", "show", "cat"),
        ["docker"] = FrozenSet.Create(StringComparer.Ordinal, "ps", "images", "logs", "inspect", "version", "info", "stats"),
        ["kubectl"] = FrozenSet.Create(StringComparer.Ordinal, "get", "describe", "logs", "version", "top"),
        ["apt"] = FrozenSet.Create(StringComparer.Ordinal, "list", "show", "search", "policy"),
        ["dpkg"] = FrozenSet.Create(StringComparer.Ordinal, "-l", "-s", "-L", "--list", "--status"),
        ["rpm"] = FrozenSet.Create(StringComparer.Ordinal, "-q", "-qa", "-qi", "-ql"),
        ["brew"] = FrozenSet.Create(StringComparer.Ordinal, "list", "info", "search", "outdated", "config", "doctor"),
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly string[] s_trustMarkers = ["known_hosts", "authorized_keys", ".ssh/config", "ssh-keygen", "ssh-copy-id", "ssh-add", "ssh_config", "sshd_config"];

    private static readonly char[] s_separators = ['|', ';', '&'];

    public static CommandRisk Classify(string command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (s_trustMarkers.Any(marker => command.Contains(marker, StringComparison.Ordinal)))
        {
            return CommandRisk.AlwaysAsk;
        }

        var hasSideEffectSyntax = command.Contains('>', StringComparison.Ordinal) || command.Contains('`', StringComparison.Ordinal) || command.Contains("$(", StringComparison.Ordinal)
            || command.Contains("<(", StringComparison.Ordinal);
        if (hasSideEffectSyntax)
        {
            return CommandRisk.Changes;
        }

        var parts = command.Split(s_separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > 0 && parts.All(IsReadOnlyPart) ? CommandRisk.ReadOnly : CommandRisk.Changes;
    }

    private static bool IsReadOnlyPart(string part)
    {
        var words = part.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var index = 0;
        while (index < words.Length && words[index].Contains('=', StringComparison.Ordinal) && !words[index].StartsWith('-'))
        {
            index++;
        }

        if (index >= words.Length)
        {
            return false;
        }

        var program = Path.GetFileName(words[index]);
        if (s_readOnly.Contains(program))
        {
            var extra = s_writingOptionsByProgram.GetValueOrDefault(program, []);
            return !words.Skip(index + 1).Any(word => IsWritingOption(word) || extra.Contains(word, StringComparer.Ordinal));
        }

        return index + 1 < words.Length
            && s_readOnlySubcommands.TryGetValue(program, out var subcommands)
            && subcommands.Contains(words[index + 1])
            && !words.Skip(index + 2).Any(IsWritingOption);
    }

    private static bool IsWritingOption(string word) =>
        s_writingOptions.Any(option => string.Equals(word, option, StringComparison.Ordinal) || (option.StartsWith("--", StringComparison.Ordinal) && word.StartsWith(option, StringComparison.Ordinal)));
}
