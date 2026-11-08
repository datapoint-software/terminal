using System.Text;
using System.Threading.Channels;
using Datapoint.Terminal.Sessions;

namespace Datapoint.Terminal.Agent.Tests;

// Product decision 141: a reopened tab's earlier output isn't live, so the agent doesn't read it as the terminal's state.
public sealed class ConnectionTerminalRestoredTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly byte[] s_prelude = Encoding.UTF8.GetBytes(
        "\e[2mroot@swarm-c:~# ls /var/run/reboot-required\r\nls: cannot access '/var/run/reboot-required'\r\n\e[22m\r\n"
        + "\e[2m── Yesterday at 21:10 · earlier session, not live ──\e[22m\r\n\r\n");

    [Fact]
    public async Task Reads_only_the_live_part_of_a_reopened_tab()
    {
        await using var session = new ScriptedSession();
        await using var connection = new TerminalConnection(session, 80, 24, s_prelude);
        var terminal = new ConnectionTerminal(connection);
        var start = terminal.GetState().NextOutputLine;
        session.Print("root@swarm-c:~# df -h /\r\n/dev/sda1  38G  12G  25G  32% /\r\nroot@swarm-c:~# ");
        for (var attempt = 0; attempt < 200 && !terminal.GetVisibleText().Contains("32%", StringComparison.Ordinal); attempt++)
        {
            await Task.Delay(10, Token);
        }

        var screen = terminal.GetVisibleText();

        Assert.Equal(
            ConnectionTerminal.RestoredNote(5) + "root@swarm-c:~# df -h /\n/dev/sda1  38G  12G  25G  32% /\nroot@swarm-c:~#",
            screen);
        Assert.DoesNotContain("earlier session, not live ──", screen, StringComparison.Ordinal);
        Assert.DoesNotContain("reboot-required", terminal.GetTextSince(0), StringComparison.Ordinal);
        Assert.StartsWith("root@swarm-c:~# df -h /", terminal.GetTextSince(start), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reads_the_whole_screen_when_nothing_was_restored()
    {
        await using var session = new ScriptedSession();
        await using var connection = new TerminalConnection(session, 80, 24);
        var terminal = new ConnectionTerminal(connection);
        session.Print("root@swarm-c:~# ");
        for (var attempt = 0; attempt < 200 && terminal.GetVisibleText().Length == 0; attempt++)
        {
            await Task.Delay(10, Token);
        }

        Assert.Equal("root@swarm-c:~#", terminal.GetVisibleText().TrimEnd());
    }

    private sealed class ScriptedSession : ITerminalSession
    {
        private readonly Channel<byte[]> _output = Channel.CreateUnbounded<byte[]>();
        private readonly TaskCompletionSource<int?> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string? ShellIntegrationNonce => null;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD003", Justification = "Completed by the test itself.")]
        public Task<int?> Exited => _exited.Task;

        public void Print(string text) => _output.Writer.TryWrite(Encoding.UTF8.GetBytes(text));

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (!await _output.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false) || !_output.Reader.TryRead(out var data))
            {
                return 0;
            }

            data.CopyTo(buffer);
            return data.Length;
        }

        public Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ResizeAsync(int columns, int rows, CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            _exited.TrySetResult(null);
            _output.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }
}
