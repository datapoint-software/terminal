using System.Globalization;
using System.Text;
using Datapoint.Terminal.Sessions.Ssh;

namespace Datapoint.Terminal.Agent.Files;

// A remote machine, through commands on the terminal's own SSH connection. The scripts run under sh whatever the login shell is,
// and a status letter before the content says whether the file exists and can be written.
public sealed class SshHostFiles(SshClient client) : IHostFiles
{
    public async Task<HostFile> ReadAsync(string path, int maxBytes, CancellationToken cancellationToken)
    {
        var script = $$"""
            p={{ShellText.QuotePath(path)}}
            if [ -e "$p" ]; then
              [ -d "$p" ] && exit 5
              [ -r "$p" ] || exit 4
              if [ -w "$p" ]; then printf W; else printf R; fi
              head -c {{(maxBytes + 1).ToString(CultureInfo.InvariantCulture)}} -- "$p"
            else
              d=$(dirname -- "$p")
              [ -d "$d" ] || exit 6
              if [ -w "$d" ]; then printf N; else printf n; fi
            fi
            """;
        var result = await client.RunAsync("sh -c " + ShellText.Quote(script), ReadOnlyMemory<byte>.Empty, maxBytes + 2, cancellationToken).ConfigureAwait(false);
        return Parse(result, maxBytes);
    }

    public async Task<string> StageAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        var name = Guid.NewGuid().ToString("N");
        var target = HostFolders.Staging + "/" + name;
        var script = $"umask 077 && mkdir -p {ShellText.QuotePath(HostFolders.Staging)} && cat > {ShellText.QuotePath(target)}";
        var result = await client.RunAsync("sh -c " + ShellText.Quote(script), content, 0, cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 0 ? target : throw new IOException(Problem(result, "couldn't save the new content"));
    }

    private static HostFile Parse(SshCommandResult result, int maxBytes)
    {
        var status = result.Output.Length > 0 ? (char)result.Output[0] : '\0';
        return (result.ExitCode, status) switch
        {
            (0, 'W' or 'R') => new HostFile(Exists: true, result.Output[1..Math.Min(result.Output.Length, maxBytes + 1)], status == 'W', result.Output.Length > maxBytes + 1),
            (0, 'N' or 'n') => HostFile.Missing(status == 'N'),
            (4, _) => throw new IOException("It can't be read with the connection's user."),
            (5, _) => throw new IOException("It's a folder, not a file."),
            (6, _) => throw new IOException("Its folder doesn't exist."),
            _ => throw new IOException(Problem(result, "couldn't read it")),
        };
    }

    private static string Problem(SshCommandResult result, string what)
    {
        var error = result.Error.Trim();
        var code = result.ExitCode is { } exit ? exit.ToString(CultureInfo.InvariantCulture) : "none";
        return error.Length == 0 ? $"The server {what} (exit code {code})." : $"The server {what}: {error}";
    }
}
