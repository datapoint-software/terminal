namespace Datapoint.Terminal.Agent;

// Commands that open an interactive session inside the terminal's shell, such as ssh to another host. While one runs, the
// shell's integration reports a command in progress, but what's on screen is the inner session's prompt, which the agent can
// type into as it does in a shell without integration.
public static class NestedSession
{
    // ssh options that take a value, so the value isn't mistaken for the destination.
    private const string SshOptionsWithValue = "BbcDEeFIiJLlmOoPpQRSWw";

    private static readonly string[] s_shells = ["sh", "bash", "zsh", "fish", "dash", "ksh", "tcsh", "csh", "pwsh"];

    // What kind of session the command line opens, e.g. "ssh session to web1", or null for an ordinary command.
    public static string? Describe(string? commandLine)
    {
        var words = Words(commandLine);
        if (words.Count == 0)
        {
            return null;
        }

        var program = Path.GetFileName(words[0]);
        var rest = words.Skip(1).ToList();
        return program switch
        {
            "ssh" or "autossh" => SshDestination(rest) is { } destination ? $"ssh session to {destination}" : null,
            "mosh" or "telnet" or "et" => rest.LastOrDefault(word => !word.StartsWith('-')) is { } host ? $"{program} session to {host}" : $"{program} session",
            "docker" or "podman" or "nerdctl" => IsInteractiveExec(rest, "exec", "run") ? "container shell" : null,
            "kubectl" or "oc" => IsInteractiveExec(rest, "exec", "debug") ? "pod shell" : null,
            "su" or "machinectl" => "nested shell",
            "sudo" or "doas" => DescribeElevated(rest),
            _ when s_shells.Contains(program, StringComparer.Ordinal) && rest.All(word => word is "-l" or "--login" or "-i") => "nested shell",
            _ => null,
        };
    }

    private static string? DescribeElevated(List<string> rest)
    {
        if (rest.Any(word => word is "-i" or "-s" or "--login" or "--shell"))
        {
            return "nested shell";
        }

        var command = rest.SkipWhile(word => word.StartsWith('-')).ToList();
        return command.Count > 0 ? Describe(string.Join(' ', command)) : null;
    }

    // ssh [options] destination [command]: interactive only without a command, so null when there's one.
    private static string? SshDestination(List<string> arguments)
    {
        var index = 0;
        while (index < arguments.Count && arguments[index].StartsWith('-') && arguments[index].Length > 1)
        {
            var option = arguments[index];
            index += option.Length == 2 && SshOptionsWithValue.Contains(option[1], StringComparison.Ordinal) ? 2 : 1;
        }

        return arguments.Count - index == 1 ? arguments[index] : null;
    }

    private static bool IsInteractiveExec(List<string> arguments, params string[] verbs) =>
        arguments.Count > 0
        && verbs.Contains(arguments[0], StringComparer.Ordinal)
        && arguments.Any(word => word is "-it" or "-ti" or "-i" or "--interactive" or "--stdin");

    // Shell words, with quotes kept together, skipping leading VAR=value assignments and exec.
    private static List<string> Words(string? commandLine)
    {
        var words = Split(commandLine ?? string.Empty);
        while (words.Count > 0 && (string.Equals(words[0], "exec", StringComparison.Ordinal) || (words[0].Contains('=', StringComparison.Ordinal) && !words[0].StartsWith('-'))))
        {
            words.RemoveAt(0);
        }

        return words;
    }

    private static List<string> Split(string text)
    {
        var words = new List<string>();
        var word = new System.Text.StringBuilder();
        char? quote = null;
        foreach (var character in text)
        {
            if (quote is null && char.IsWhiteSpace(character))
            {
                Flush(words, word);
                continue;
            }

            if (character is '\'' or '"' && (quote is null || quote == character))
            {
                quote = quote is null ? character : null;
            }

            word.Append(character);
        }

        Flush(words, word);
        return words;
    }

    private static void Flush(List<string> words, System.Text.StringBuilder word)
    {
        if (word.Length == 0)
        {
            return;
        }

        words.Add(word.ToString());
        word.Clear();
    }
}
