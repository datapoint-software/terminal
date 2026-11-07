using System.Globalization;

namespace Datapoint.Terminal.Sessions.Ssh.Config;

// A local forward, as LocalForward writes it: [bind_address:]port host:hostport, with IPv6 addresses in brackets. Forwards to
// Unix sockets aren't supported.
public sealed record SshForward(string? BindAddress, int LocalPort, string Host, int Port)
{
    public override string ToString() => $"{LocalPort} {(Host.Contains(':', StringComparison.Ordinal) ? $"[{Host}]" : Host)}:{Port}";

    // Whether the bind address asks for more than this computer; forwards here listen only on the loopback address.
    public bool AsksForNetwork => BindAddress is { } address
        && !string.Equals(address, "localhost", StringComparison.OrdinalIgnoreCase)
        && address is not ("127.0.0.1" or "::1");

    public static SshForward? Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var parts = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || Split(parts[1]) is not ({ Length: > 0 } host, { } port) || port == 0 || host[0] == '-')
        {
            return null;
        }

        var (bind, local) = parts[0].Any(character => character is ':' or '[') ? Split(parts[0]) : (null, ParsePort(parts[0]));
        return local is null or 0 || host.Any(char.IsWhiteSpace) || host.Contains('/', StringComparison.Ordinal)
            ? null
            : new SshForward(bind is { Length: > 0 } ? bind : null, local.Value, host, port);
    }

    private static (string? Host, int? Port) Split(string text)
    {
        if (text.StartsWith('['))
        {
            var close = text.IndexOf(']', StringComparison.Ordinal);
            return close > 0 && close + 1 < text.Length && text[close + 1] == ':' ? (text[1..close], ParsePort(text[(close + 2)..])) : (null, null);
        }

        var colon = text.LastIndexOf(':');
        return colon < 0 ? (null, null) : (text[..colon], ParsePort(text[(colon + 1)..]));
    }

    private static int? ParsePort(string text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port is > 0 and < 65536 ? port : null;
}
