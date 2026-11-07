using System.Collections.Frozen;

namespace Datapoint.Terminal.Agent.Safety;

// Conservative: a command is read-only only when every part of it is a reading command listed in ReadOnlyCommands.jsonc,
// with no output redirection or writing option. Anything unknown counts as a change.
public static class CommandClassifier
{
    // Programs that reach other machines, and so can carry data off this one: a name looked up with dig or ping goes to a DNS
    // server the name's owner runs. Matched anywhere in the command, since they're also run through sudo, xargs or sh -c.
    private static readonly FrozenSet<string> s_network = FrozenSet.Create(
        StringComparer.Ordinal,
        "curl", "wget", "dig", "nslookup", "drill", "ping", "ping6", "traceroute", "tracepath", "mtr", "nc", "ncat", "netcat", "socat",
        "telnet", "ssh", "scp", "sftp", "rsync", "ftp", "lftp", "aria2c", "whois");

    // Network programs whose names are also common words, so they count only where a command starts.
    private static readonly FrozenSet<string> s_networkAtStart = FrozenSet.Create(StringComparer.Ordinal, "host", "http", "https", "xh");

    private static readonly FrozenSet<string> s_gitNetwork = FrozenSet.Create(StringComparer.Ordinal, "push", "pull", "fetch", "clone", "ls-remote", "submodule");

    private static readonly FrozenSet<string> s_harmlessVariables = FrozenSet.Create(
        StringComparer.Ordinal, "LANG", "LANGUAGE", "TZ", "COLUMNS", "LINES", "TERM", "NO_COLOR", "CLICOLOR", "SYSTEMD_COLORS");

    private static readonly FrozenSet<string> s_pagerVariables = FrozenSet.Create(StringComparer.Ordinal, "PAGER", "GIT_PAGER", "SYSTEMD_PAGER", "MANPAGER");

    private static readonly FrozenSet<string> s_systemFolders = FrozenSet.Create(
        StringComparer.Ordinal, "/bin", "/sbin", "/usr/bin", "/usr/sbin", "/usr/local/bin", "/usr/local/sbin", "/opt/homebrew/bin");

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

    // Whether the command can send something to another machine. Best effort: a script or interpreter can always open a
    // connection of its own, which is why this only adds questions and never removes one.
    public static bool IsNetwork(string command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Contains("/dev/tcp/", StringComparison.Ordinal) || command.Contains("/dev/udp/", StringComparison.Ordinal))
        {
            return true;
        }

        var words = command.Split([' ', '\t', '"', '\'', '(', ')', '`', '|', ';', '&', '$', '{', '}'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Any(word => s_network.Contains(Path.GetFileName(word))))
        {
            return true;
        }

        return command.Split(s_separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Any(IsNetworkPart);
    }

    private static bool IsNetworkPart(string part)
    {
        var words = part.Split(' ', StringSplitOptions.RemoveEmptyEntries).SkipWhile(word => word is "sudo" or "doas" || (word.Contains('=', StringComparison.Ordinal) && !word.StartsWith('-'))).ToList();
        if (words.Count == 0)
        {
            return false;
        }

        var program = Path.GetFileName(words[0]);
        return s_networkAtStart.Contains(program) || (string.Equals(program, "git", StringComparison.Ordinal) && words.Skip(1).Any(s_gitNetwork.Contains));
    }

    private static bool IsReadOnlyPart(string part)
    {
        var words = part.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        var index = 0;
        while (index < words.Length && IsAssignment(words[index]))
        {
            if (!IsHarmlessAssignment(words[index]))
            {
                return false;
            }

            index++;
        }

        if (index >= words.Length || ProgramName(words[index]) is not { } program)
        {
            return false;
        }

        return FindRule(program, words, index + 1) is { } rule && rule.Permits(part, words[(index + rule.Length)..]);
    }

    private static bool IsAssignment(string word) => word.Contains('=', StringComparison.Ordinal) && !word.StartsWith('-');

    // Variables can make a reading program run something else (LD_PRELOAD, GIT_EXTERNAL_DIFF, LESSOPEN, a pager), so only
    // ones that change how output looks keep a command reading.
    private static bool IsHarmlessAssignment(string word)
    {
        var name = word[..word.IndexOf('=', StringComparison.Ordinal)];
        var value = word[(name.Length + 1)..].Trim('\'', '"');
        return s_harmlessVariables.Contains(name) || name.StartsWith("LC_", StringComparison.Ordinal)
            || (s_pagerVariables.Contains(name) && value is "" or "cat");
    }

    // A program given by its path counts only from the system's own folders: ./ls or /tmp/cat could be anything.
    private static string? ProgramName(string word)
    {
        if (word.Contains('\\', StringComparison.Ordinal))
        {
            return null;
        }

        if (!word.Contains('/', StringComparison.Ordinal))
        {
            return word;
        }

        var folder = word[..word.LastIndexOf('/')];
        return s_systemFolders.Contains(folder) ? word[(folder.Length + 1)..] : null;
    }

    // The longest listed command the words start with, e.g. "docker node ls" before "docker".
    private static CommandRule? FindRule(string program, string[] words, int start)
    {
        for (var count = Math.Min(CommandRules.MaxWords, words.Length - start + 1); count >= 1; count--)
        {
            var command = string.Join(' ', words.Skip(start).Take(count - 1).Prepend(program));
            if (CommandRules.BuiltIn.Find(command) is { } rule)
            {
                return rule;
            }
        }

        return null;
    }
}
