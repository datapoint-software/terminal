using System.Reflection;
using System.Text;
using Datapoint.Terminal.Sessions.Ssh.Protocol;

namespace Datapoint.Terminal.Sessions.Ssh.Transport;

public static class VersionExchange
{
    public const int MaxLineLength = 255;
    public const int MaxLinesBeforeVersion = 64;

    public static string ClientVersion { get; } = Identification(ProductVersion());

    // RFC 4253, section 4.2: the software version is printable ASCII with no spaces or hyphens, as servers split the line on
    // hyphens. A pre-release's hyphen becomes an underscore, e.g. "1.0.0-alpha.1" is sent as "1.0.0_alpha.1".
    public static string Identification(string version)
    {
        ArgumentNullException.ThrowIfNull(version);
        var software = new string([.. version.Select(character => character is > ' ' and < '\x7f' and not '-' ? character : '_')]);
        return $"SSH-2.0-DatapointTerminal_{software}";
    }

    public static async Task<string> RunAsync(Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        await stream.WriteAsync(Encoding.ASCII.GetBytes(ClientVersion + "\r\n"), cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        for (var line = 0; line < MaxLinesBeforeVersion; line++)
        {
            var text = await ReadLineAsync(stream, cancellationToken).ConfigureAwait(false);
            if (text.StartsWith("SSH-", StringComparison.Ordinal))
            {
                return ValidateServerVersion(text);
            }
        }

        throw new SshProtocolException("The server sent too many lines before its version.", DisconnectReason.ProtocolVersionNotSupported);
    }

    private static string ValidateServerVersion(string version)
    {
        var isSupported = version.StartsWith("SSH-2.0-", StringComparison.Ordinal) || version.StartsWith("SSH-1.99-", StringComparison.Ordinal);
        if (!isSupported)
        {
            throw new SshProtocolException("The server doesn't speak SSH version 2.", DisconnectReason.ProtocolVersionNotSupported);
        }

        return version;
    }

    private static async Task<string> ReadLineAsync(Stream stream, CancellationToken cancellationToken)
    {
        var line = new byte[MaxLineLength + 2];
        var length = 0;
        var single = new byte[1];
        while (length < line.Length)
        {
            var read = await stream.ReadAsync(single, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new SshProtocolException("The connection closed during the version exchange.", DisconnectReason.ConnectionLost);
            }

            if (single[0] == '\n')
            {
                return DecodeLine(line.AsSpan(0, length));
            }

            line[length] = single[0];
            length++;
        }

        throw new SshProtocolException("A line from the server is too long.", DisconnectReason.ProtocolError);
    }

    private static string DecodeLine(ReadOnlySpan<byte> line)
    {
        var trimmed = line.EndsWith("\r"u8) ? line[..^1] : line;
        foreach (var value in trimmed)
        {
            if (value is < 0x20 or > 0x7E)
            {
                throw new SshProtocolException("The server's version line contains control characters.", DisconnectReason.ProtocolError);
            }
        }

        return Encoding.ASCII.GetString(trimmed);
    }

    private static string ProductVersion()
    {
        var informational = typeof(VersionExchange).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return (informational ?? "0.0.0").Split('+')[0];
    }
}
