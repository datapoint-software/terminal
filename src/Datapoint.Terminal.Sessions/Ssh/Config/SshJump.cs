using System.Globalization;

namespace Datapoint.Terminal.Sessions.Ssh.Config;

// One hop of ProxyJump: [user@]host[:port] or ssh://[user@]host[:port], where host may be an alias from the configuration.
public sealed record SshJump(string Host, string? User, int? Port)
{
    public static IReadOnlyList<SshJump>? Parse(string? value, out string? problem)
    {
        problem = null;
        if (value is null || string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var hops = new List<SshJump>();
        foreach (var part in value.Split(',', StringSplitOptions.TrimEntries))
        {
            if (ParseOne(part) is not { } hop)
            {
                problem = $"\"{part}\" in ProxyJump isn't [user@]host[:port].";
                return null;
            }

            hops.Add(hop);
        }

        return hops;
    }

    private static SshJump? ParseOne(string text)
    {
        var rest = text.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase) ? text[6..] : text;
        string? user = null;
        var at = rest.LastIndexOf('@');
        if (at >= 0)
        {
            user = rest[..at];
            rest = rest[(at + 1)..];
        }

        var (host, port) = SplitPort(rest);
        return host.Length == 0 || (user is not null && user.Length == 0) || port is 0 || host.Any(character => char.IsWhiteSpace(character) || character == '/') || host[0] == '-'
            ? null
            : new SshJump(host, user, port);
    }

    // [::1]:2222 and host:2222; a bare IPv6 address has no port.
    private static (string Host, int? Port) SplitPort(string text)
    {
        if (text.StartsWith('['))
        {
            var close = text.IndexOf(']', StringComparison.Ordinal);
            return close < 0 ? (string.Empty, null)
                : close == text.Length - 1 ? (text[1..close], null)
                : text[close + 1] == ':' ? (text[1..close], ParsePort(text[(close + 2)..]))
                : (string.Empty, null);
        }

        var colon = text.IndexOf(':', StringComparison.Ordinal);
        return colon < 0 || colon != text.LastIndexOf(':') ? (text, null) : (text[..colon], ParsePort(text[(colon + 1)..]));
    }

    private static int ParsePort(string text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port is > 0 and < 65536 ? port : 0;
}
