using System.Text;
using System.Threading.Channels;

namespace Datapoint.Terminal.Sessions.Tests;

public sealed class TerminalConnectionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Carries_on_in_the_same_terminal_with_a_new_session_after_the_first_drops()
    {
        var first = new FakeSession();
        await using var connection = new TerminalConnection(first, 40, 10);
        var ended = 0;
        connection.Ended += (_, _) => Interlocked.Increment(ref ended);
        first.Print("vim\r\n\e[?1049hfull screen");
        first.Drop();
        await WaitUntilAsync(() => Volatile.Read(ref ended) == 1);

        var second = new FakeSession();
        await connection.ResumeAsync(second).WaitAsync(TimeSpan.FromSeconds(5), Token);
        second.Print("back again");
        await WaitUntilAsync(() => connection.Terminal.GetVisibleText().Contains("back again", StringComparison.Ordinal));
        connection.Send("ls\r"u8);
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(Token);
        patience.CancelAfter(TimeSpan.FromSeconds(5));
        var typed = await second.Typed.Reader.ReadAsync(patience.Token);

        // What the first session showed stays on the main screen; the program that took the alternate one is gone.
        Assert.Contains("vim", connection.Terminal.GetVisibleText(), StringComparison.Ordinal);
        Assert.DoesNotContain("full screen", connection.Terminal.GetVisibleText(), StringComparison.Ordinal);
        Assert.Equal("ls\r", Encoding.UTF8.GetString(typed));
        Assert.True(first.IsDisposed);
        Assert.False(connection.Exited.IsCompleted);

        second.Drop();
        await WaitUntilAsync(() => Volatile.Read(ref ended) == 2);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var until = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < until, "Timed out.");
            await Task.Delay(10, Token);
        }
    }

    private sealed class FakeSession : ITerminalSession
    {
        private readonly Channel<byte[]> _output = Channel.CreateUnbounded<byte[]>();
        private readonly TaskCompletionSource<int?> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Channel<byte[]> Typed { get; } = Channel.CreateUnbounded<byte[]>();

        public bool IsDisposed { get; private set; }

        public string? ShellIntegrationNonce => null;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD003", Justification = "Completed by the test itself.")]
        public Task<int?> Exited => _exited.Task;

        public void Print(string text) => _output.Writer.TryWrite(Encoding.UTF8.GetBytes(text));

        // The connection went away without the shell saying how it ended.
        public void Drop()
        {
            _exited.TrySetResult(null);
            _output.Writer.TryComplete();
        }

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (!await _output.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false) || !_output.Reader.TryRead(out var data))
            {
                return 0;
            }

            data.CopyTo(buffer);
            return data.Length;
        }

        public Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
        {
            Typed.Writer.TryWrite(data.ToArray());
            return Task.CompletedTask;
        }

        public Task ResizeAsync(int columns, int rows, CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            Drop();
            return ValueTask.CompletedTask;
        }
    }
}
