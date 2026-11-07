using System.Net;
using System.Net.Sockets;

namespace Datapoint.Terminal.Agent.Web;

// Which addresses the agent may read. The application runs on machines inside infrastructure networks, so a page the agent
// asks for must be on the public internet: never this machine, a private or link-local network, or a cloud provider's
// metadata service, which would hand internal data to the model and from there to anyone it searches with.
public static class WebAddressGuard
{
    // IPv4 ranges that aren't the public internet (IANA special-purpose registry).
    private static readonly (IPAddress Network, int Bits)[] s_closedV4 =
    [
        (IPAddress.Parse("0.0.0.0"), 8), // this network
        (IPAddress.Parse("10.0.0.0"), 8),
        (IPAddress.Parse("100.64.0.0"), 10), // shared address space (carrier-grade NAT)
        (IPAddress.Parse("127.0.0.0"), 8),
        (IPAddress.Parse("169.254.0.0"), 16), // link-local, where cloud metadata services live
        (IPAddress.Parse("172.16.0.0"), 12),
        (IPAddress.Parse("192.0.0.0"), 24), // protocol assignments
        (IPAddress.Parse("192.0.2.0"), 24), // documentation
        (IPAddress.Parse("192.168.0.0"), 16),
        (IPAddress.Parse("198.18.0.0"), 15), // benchmarking
        (IPAddress.Parse("198.51.100.0"), 24),
        (IPAddress.Parse("203.0.113.0"), 24),
        (IPAddress.Parse("224.0.0.0"), 3), // multicast, reserved and broadcast
    ];

    // Inside global unicast (2000::/3), the ranges set aside or that tunnel to an address judged elsewhere.
    private static readonly (IPAddress Network, int Bits)[] s_closedV6 =
    [
        (IPAddress.Parse("2001::"), 32), // Teredo
        (IPAddress.Parse("2001:db8::"), 32), // documentation
    ];

    private static readonly (IPAddress Network, int Bits) s_globalV6 = (IPAddress.Parse("2000::"), 3);

    private static readonly (IPAddress Network, int Bits) s_sixToFour = (IPAddress.Parse("2002::"), 16);

    private static readonly string[] s_localSuffixes = [".localhost", ".local", ".internal", ".home.arpa", ".lan"];

    // The reason an address can't be read, or null when it can be, before its host is looked up.
    public static string? Check(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return CheckForm(address) ?? CheckHost(address);
    }

    // Public unicast only. Mapped and embedded IPv4 addresses are judged as IPv4, so ::ffff:127.0.0.1 is loopback.
    public static bool IsPublic(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => !s_closedV4.Any(range => IsIn(address, range)),
            AddressFamily.InterNetworkV6 => IsPublicV6(address),
            _ => false,
        };
    }

    private static string? CheckForm(Uri address)
    {
        if (!address.IsAbsoluteUri || !IsWebScheme(address.Scheme))
        {
            return "Only http and https addresses can be read.";
        }

        if (!string.IsNullOrEmpty(address.UserInfo))
        {
            return "Addresses with a user name or password can't be read.";
        }

        return address.HostNameType is UriHostNameType.Basic or UriHostNameType.Unknown ? "The address has no valid host." : null;
    }

    private static string? CheckHost(Uri address)
    {
        var host = address.IdnHost.TrimEnd('.');
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || s_localSuffixes.Any(suffix => host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
        {
            return "Addresses on this machine or its local network can't be read.";
        }

        if (address.HostNameType is UriHostNameType.Dns && !host.Contains('.', StringComparison.Ordinal))
        {
            return "Addresses on the local network can't be read.";
        }

        return IPAddress.TryParse(host.Trim('[', ']'), out var literal) && !IsPublic(literal)
            ? "Addresses on this machine or a private network can't be read."
            : null;
    }

    public static bool IsWebScheme(string scheme) =>
        string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) || string.Equals(scheme, Uri.UriSchemeHttp, StringComparison.Ordinal);

    // Loopback, link-local, unique-local, multicast and NAT64 all fall outside 2000::/3; 6to4 carries an IPv4 address.
    private static bool IsPublicV6(IPAddress address)
    {
        if (!IsIn(address, s_globalV6) || s_closedV6.Any(range => IsIn(address, range)))
        {
            return false;
        }

        if (!IsIn(address, s_sixToFour))
        {
            return true;
        }

        var bytes = address.GetAddressBytes();
        return IsPublic(new IPAddress(bytes.AsSpan(2, 4)));
    }

    private static bool IsIn(IPAddress address, (IPAddress Network, int Bits) range)
    {
        if (address.AddressFamily != range.Network.AddressFamily)
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        var network = range.Network.GetAddressBytes();
        var whole = range.Bits / 8;
        if (!bytes.AsSpan(0, whole).SequenceEqual(network.AsSpan(0, whole)))
        {
            return false;
        }

        var rest = range.Bits % 8;
        var mask = (byte)(0xFF << (8 - rest));
        return rest == 0 || (bytes[whole] & mask) == (network[whole] & mask);
    }
}
