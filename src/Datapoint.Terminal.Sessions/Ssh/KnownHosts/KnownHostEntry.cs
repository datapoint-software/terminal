namespace Datapoint.Terminal.Sessions.Ssh.KnownHosts;

internal sealed record KnownHostEntry(IReadOnlyList<string> Patterns, string KeyType, byte[] KeyBlob, bool IsRevoked, string Source, int Line)
{
    public static KnownHostEntry? Parse(string text, string source, int line)
    {
        var fields = text.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length == 0 || fields[0].StartsWith('#'))
        {
            return null;
        }

        var isRevoked = string.Equals(fields[0], "@revoked", StringComparison.Ordinal);
        if (fields[0].StartsWith('@') && !isRevoked)
        {
            return null;
        }

        var offset = isRevoked ? 1 : 0;
        if (fields.Length < offset + 3)
        {
            return null;
        }

        var blob = new byte[fields[offset + 2].Length];
        if (!Convert.TryFromBase64String(fields[offset + 2], blob, out var length))
        {
            return null;
        }

        return new KnownHostEntry(fields[offset].Split(','), fields[offset + 1], blob[..length], isRevoked, source, line);
    }

    public bool Matches(string name)
    {
        if (Patterns.Count == 1 && Patterns[0].StartsWith("|1|", StringComparison.Ordinal))
        {
            return MatchesHashed(Patterns[0], name);
        }

        return KnownHostsFile.IsPatternMatch(name, Patterns);
    }

    private static bool MatchesHashed(string hashed, string name)
    {
        var parts = hashed.Split('|');
        if (parts.Length != 4)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            return KnownHostsFile.Hmac(salt, name).AsSpan().SequenceEqual(expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
