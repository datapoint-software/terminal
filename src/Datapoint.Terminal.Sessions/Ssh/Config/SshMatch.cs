namespace Datapoint.Terminal.Sessions.Ssh.Config;

// Match blocks as OpenSSH evaluates them, in a single pass. "exec" is never run: it would run a command from the file
// whenever a host is looked up, so it counts as not matching and the user is told.
internal static class SshMatch
{
    public static bool Evaluate(SshConfigLine line, ResolveState state)
    {
        var arguments = line.Arguments;
        var index = 0;
        var result = true;
        while (index < arguments.Count)
        {
            var criterion = arguments[index++].ToLowerInvariant();
            var negate = criterion.StartsWith('!');
            criterion = negate ? criterion[1..] : criterion;
            if (criterion is "all" or "canonical" or "final")
            {
                result &= negate == string.Equals(criterion, "canonical", StringComparison.Ordinal);
                continue;
            }

            if (index >= arguments.Count)
            {
                state.Problems.Add($"{line.Source}, line {line.Number}: Match {criterion} needs a value.");
                return false;
            }

            var value = arguments[index++];
            result &= negate != Criterion(criterion, value, line, state);
        }

        return result;
    }

    private static bool Criterion(string criterion, string value, SshConfigLine line, ResolveState state)
    {
        var patterns = value.Split(',');
        switch (criterion)
        {
            case "host":
                return SshPattern.IsListMatch(state.HostName, patterns);
            case "originalhost":
                return SshPattern.IsListMatch(state.Alias, patterns);
            case "user":
                return SshPattern.IsListMatch(state.User, patterns);
            case "localuser":
                return SshPattern.IsListMatch(state.LocalUser, patterns);
            default:
                state.Problems.Add($"{line.Source}, line {line.Number}: Match {criterion} isn't supported, so that block is skipped.");
                return false;
        }
    }
}
