using System.Text;

namespace Datapoint.Terminal.Sessions.Ssh;

// Commands on a channel of their own, beside the terminal: exact output, separate errors and a real exit code (product
// decision 57). The terminal's shell, its state and the user's typing are untouched.
public sealed partial class SshClient
{
    private const int MaxErrorBytes = 16 * 1024;

    public async Task<SshCommandResult> RunAsync(string command, ReadOnlyMemory<byte> input, int maxOutput, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(command);
        ArgumentOutOfRangeException.ThrowIfNegative(maxOutput);
        var channel = await OpenSessionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await channel.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
            if (!input.IsEmpty)
            {
                await channel.WriteAsync(input, cancellationToken).ConfigureAwait(false);
            }

            await channel.SendEofAsync(cancellationToken).ConfigureAwait(false);
            using var output = new MemoryStream();
            using var error = new MemoryStream();
            var isTruncated = false;
            while (await channel.ReadAsync(cancellationToken).ConfigureAwait(false) is { } data)
            {
                var target = data.IsError ? error : output;
                var room = (data.IsError ? MaxErrorBytes : maxOutput) - (int)target.Length;
                isTruncated |= !data.IsError && data.Data.Length > room;
                target.Write(data.Data.Span[..Math.Min(Math.Max(room, 0), data.Data.Length)]);
            }

            await channel.Completion.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new SshCommandResult(output.ToArray(), Encoding.UTF8.GetString(error.ToArray()), channel.ExitCode, isTruncated);
        }
        finally
        {
            await channel.CloseAsync(CancellationToken.None).ConfigureAwait(false);
            channel.Dispose();
        }
    }
}
