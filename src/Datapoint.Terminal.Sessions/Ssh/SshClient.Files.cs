using System.Globalization;
using System.Text;
using Datapoint.Terminal.Sessions.Ssh.Channels;

namespace Datapoint.Terminal.Sessions.Ssh;

// File transfer for the user, through cat on a channel beside the terminal, so it works on any POSIX host without SFTP. An upload
// goes to a temporary name first and replaces the target only once complete, so an interrupted one leaves the old file intact.
public sealed partial class SshClient
{
    public const int ExitExists = 17;
    public const int ExitDirectory = 18;

    public async Task<long> DownloadAsync(string remotePath, Stream destination, IProgress<long>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var channel = await OpenSessionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await channel.ExecuteAsync("cat -- " + QuoteRemotePath(remotePath), cancellationToken).ConfigureAwait(false);
            await channel.SendEofAsync(cancellationToken).ConfigureAwait(false);
            var errors = new StringBuilder();
            long total = 0;
            while (await channel.ReadAsync(cancellationToken).ConfigureAwait(false) is { } data)
            {
                if (data.IsError)
                {
                    AppendError(errors, data.Data.Span);
                    continue;
                }

                await destination.WriteAsync(data.Data, cancellationToken).ConfigureAwait(false);
                total += data.Data.Length;
                progress?.Report(total);
            }

            await channel.Completion.WaitAsync(cancellationToken).ConfigureAwait(false);
            return channel.ExitCode == 0 ? total : throw new IOException(TransferProblem(channel, errors));
        }
        finally
        {
            await CloseQuietlyAsync(channel).ConfigureAwait(false);
        }
    }

    public async Task<long> UploadAsync(Stream source, string remotePath, bool overwrite, IProgress<long>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        var target = QuoteRemotePath(remotePath);
        var replace = overwrite ? 1 : 0;
        var script = string.Create(
            CultureInfo.InvariantCulture,
            $$"""target={{target}}; partial="$target.datapoint-partial"; if [ -d "$target" ]; then exit {{ExitDirectory}}; fi; if [ -e "$target" ] && [ {{replace}} = 0 ]; then exit {{ExitExists}}; fi; if cat > "$partial"; then mv -f -- "$partial" "$target"; else status=$?; rm -f -- "$partial"; exit $status; fi""");
        var channel = await OpenSessionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await channel.ExecuteAsync(script, cancellationToken).ConfigureAwait(false);
            var errors = new StringBuilder();
            var reading = ReadErrorsAsync(channel, errors, cancellationToken);
            long total = 0;
            SshChannelException? closed = null;
            try
            {
                total = await SendAsync(source, channel, progress, cancellationToken).ConfigureAwait(false);
                await channel.SendEofAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (SshChannelException exception)
            {
                // The script can end before reading anything, e.g. when the file exists, and close the channel while the file
                // is still being sent. Its exit code then says why.
                closed = exception;
            }

            await reading.ConfigureAwait(false);
            await channel.Completion.WaitAsync(cancellationToken).ConfigureAwait(false);
            return channel.ExitCode switch
            {
                ExitExists => throw new RemoteFileExistsException(),
                null or 0 when closed is not null => throw closed,
                0 => total,
                _ => throw new IOException(TransferProblem(channel, errors)),
            };
        }
        finally
        {
            await CloseQuietlyAsync(channel).ConfigureAwait(false);
        }
    }

    // A leading ~/ stays outside the quotes so the remote shell expands it to the home folder.
    public static string QuoteRemotePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return path.StartsWith("~/", StringComparison.Ordinal) ? "~/" + Quote(path[2..]) : Quote(path);
    }

    private static string Quote(string text) => "'" + text.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    private static async Task<long> SendAsync(Stream source, SshChannel channel, IProgress<long>? progress, CancellationToken cancellationToken)
    {
        var buffer = new byte[SshChannel.MaxPacket];
        long total = 0;
        int count;
        while ((count = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await channel.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
            total += count;
            progress?.Report(total);
        }

        return total;
    }

    // Read while sending, so a failure the shell reports early (e.g. permission denied) doesn't leave the window full.
    private static async Task ReadErrorsAsync(SshChannel channel, StringBuilder errors, CancellationToken cancellationToken)
    {
        while (await channel.ReadAsync(cancellationToken).ConfigureAwait(false) is { } data)
        {
            AppendError(errors, data.Data.Span);
        }
    }

    private static void AppendError(StringBuilder errors, ReadOnlySpan<byte> data)
    {
        if (errors.Length >= 2048)
        {
            return;
        }

        errors.Append(Encoding.UTF8.GetString(data));
    }

    private static string TransferProblem(SshChannel channel, StringBuilder errors) => channel.ExitCode switch
    {
        ExitDirectory => "That path is a folder; name the file to write.",
        _ => ServerText.Sanitize(errors.ToString().Trim(), 512) is { Length: > 0 } text ? text : $"The transfer failed (exit code {channel.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "none"}).",
    };

    private static async Task CloseQuietlyAsync(SshChannel channel)
    {
        try
        {
            await channel.CloseAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is SshChannelException or SshDisconnectedException or IOException)
        {
            // Closed with the connection.
        }

        channel.Dispose();
    }
}
