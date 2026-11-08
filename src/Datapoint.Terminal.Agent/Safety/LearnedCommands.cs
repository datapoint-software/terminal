using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace Datapoint.Terminal.Agent.Safety;

// Commands the user taught the agent to treat as read-only (product decision 112), kept in their own settings and never in a
// workspace's shared files, so nothing someone else commits can widen what runs without asking. A taught command still
// counts as a change with a writing option, output redirection or a variable that could run something else.
public sealed partial class LearnedCommands
{
    // Programs that run other commands or code, change things whatever their arguments, or reach other machines.
    private static readonly FrozenSet<string> s_neverPrograms = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "sudo", "doas", "su", "pkexec", "runas", "env", "nice", "ionice", "time", "timeout", "xargs", "watch", "nohup", "setsid",
        "stdbuf", "unbuffer", "chroot", "nsenter", "unshare", "flock", "strace", "ltrace", "gdb", "lldb", "script", "expect",
        "parallel", "sh", "bash", "zsh", "fish", "dash", "ksh", "csh", "tcsh", "pwsh", "powershell", "cmd", "python", "python2",
        "python3", "perl", "ruby", "node", "nodejs", "deno", "bun", "php", "lua", "Rscript", "osascript", "awk", "gawk", "mawk",
        "nawk", "sed", "vi", "vim", "nvim", "emacs", "nano", "less", "more", "man", "make", "eval", "exec", "source", "busybox",
        "systemd-run", "tmux", "screen", "xdg-open", "open", "start", "npx", "Invoke-Expression", "iex", "Start-Process",
        "Invoke-Command", "Invoke-Item", "rm", "rmdir", "dd", "mkfs", "shred", "wipefs", "fdisk", "parted", "kill", "killall",
        "pkill", "reboot", "shutdown", "poweroff", "halt", "chmod", "chown", "mv", "cp", "ln", "mkdir", "touch", "tee", "install",
        "truncate");

    // Subcommands that run something or change things, whichever program they belong to.
    private static readonly FrozenSet<string> s_neverWords = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "exec", "run", "run-script", "start", "stop", "restart", "reload", "kill", "attach", "cp", "mv", "rm", "rmi", "delete", "del",
        "remove", "destroy", "drop", "purge", "prune", "create", "add", "set", "unset", "update", "upgrade", "install", "uninstall",
        "apply", "patch", "replace", "edit", "scale", "drain", "cordon", "uncordon", "reset", "enable", "disable", "mask", "unmask",
        "push", "pull", "deploy", "rollback", "undo", "restore", "shell", "ssh", "debug", "eval", "port-forward", "proxy", "console",
        "repl", "x", "dlx", "test", "sh", "bash", "init", "import", "load", "mount", "umount", "format", "write", "put", "post",
        "send", "sync", "clean", "clear", "flush", "commit", "merge", "rebase", "checkout", "switch", "login", "logout");

    private readonly FrozenDictionary<string, CommandRule> _rules;

    private LearnedCommands(IEnumerable<string> commands)
    {
        Commands = [.. commands.Select(Normalize).OfType<string>().Distinct(StringComparer.Ordinal)];
        _rules = Commands.ToFrozenDictionary(
            command => command,
            command => new CommandRule(command, CommandRules.BuiltIn.WritesFor(command.Split(' ')[0])),
            StringComparer.Ordinal);
    }

    public static LearnedCommands Empty { get; } = new([]);

    // In the form Normalize gives; anything that doesn't pass it, e.g. from a hand-edited settings file, is left out.
    public IReadOnlyList<string> Commands { get; }

    public static LearnedCommands From(IEnumerable<string> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        return new LearnedCommands(commands);
    }

    internal CommandRule? Find(string command) => _rules.GetValueOrDefault(command);

    // The command as it's kept, e.g. "docker  swarm ca" as "docker swarm ca", or null when it can't be taught: more than
    // three words, a path, anything but plain words and options, or a program or subcommand that runs or changes things.
    public static string? Normalize(string? text)
    {
        var words = (text ?? string.Empty).Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length is 0 or > CommandRules.MaxWords || !ProgramWord().IsMatch(words[0]) || s_neverPrograms.Contains(words[0]))
        {
            return null;
        }

        if (words.Skip(1).Any(word => !(SubcommandWord().IsMatch(word) || OptionWord().IsMatch(word)) || s_neverWords.Contains(word)))
        {
            return null;
        }

        var command = string.Join(' ', words);
        return CommandClassifier.IsNetwork(command) || CommandClassifier.Classify(command) == CommandRisk.AlwaysAsk ? null : command;
    }

    // What to offer when the user approves a command the check counted as a change: the program and the subcommands it starts
    // with, e.g. "docker swarm ca" for "docker swarm ca --rotate=false". Null when there's nothing safe to offer, including when
    // the command would still count as a change once taught, e.g. for a writing option.
    public static string? Suggest(string command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.IndexOfAny(['|', ';', '&', '\n', '<', '>', '`', '$', '(', '{']) >= 0 || CommandClassifier.IsNetwork(command)
            || CommandClassifier.Classify(command) != CommandRisk.Changes)
        {
            return null;
        }

        var all = command.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        var start = CommandClassifier.CommandStart(all);
        if (start < 0)
        {
            return null;
        }

        // What's taught is the command itself, so it reads with sudo or without.
        var words = all[start..];

        var subcommands = words.Skip(1).TakeWhile(word => SubcommandWord().IsMatch(word)).Take(CommandRules.MaxWords - 1).Count();

        // Without subcommands, teaching the whole program would also cover "tool --verbose delete": offer it only when
        // nothing that could be one follows.
        if (subcommands == 0 && words.Skip(1).Any(word => SubcommandWord().IsMatch(word)))
        {
            return null;
        }

        var suggestion = Normalize(string.Join(' ', words.Take(subcommands + 1)));
        return suggestion is not null && CommandClassifier.Classify(command, From([suggestion])) == CommandRisk.ReadOnly ? suggestion : null;
    }

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._+-]*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex ProgramWord();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9._:-]*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex SubcommandWord();

    [GeneratedRegex(@"^--?[A-Za-z0-9][A-Za-z0-9-]*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex OptionWord();
}
