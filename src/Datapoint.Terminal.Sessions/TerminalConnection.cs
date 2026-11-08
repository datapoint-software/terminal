using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using Datapoint.Terminal.Emulator;
using EmulatorTerminal = Datapoint.Terminal.Emulator.Terminal;

namespace Datapoint.Terminal.Sessions;

public sealed class TerminalConnection : ITerminalHost, IAsyncDisposable
{
    private ITerminalSession _session;
    private Channel<byte[]> _input = NewInput();
    private CancellationTokenSource _lifetime = new();
    private Task _reader;
    private Task _writer;

    // The prelude, such as a reopened tab's earlier output, is shown before anything the session prints.
    public TerminalConnection(ITerminalSession session, int columns, int rows, ReadOnlyMemory<byte> prelude = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        Terminal = new EmulatorTerminal(columns, rows, this) { ShellIntegrationNonce = session.ShellIntegrationNonce };
        Terminal.Feed(prelude.Span);
        LiveFromLine = Terminal.Primary.FirstVisibleLine + Terminal.Cursor.Row;
        Terminal.LinesMoved += (_, e) => LiveFromLine = e.Map.Map(LiveFromLine);
        _reader = Task.Run(() => ReadLoopAsync(_lifetime.Token), CancellationToken.None);
        _writer = Task.Run(() => WriteLoopAsync(_lifetime.Token), CancellationToken.None);
    }

    public EmulatorTerminal Terminal { get; }

    // The first line the session itself wrote, after any prelude; moved when the text reflows.
    public long LiveFromLine { get; private set; }

    // These events are raised on a background thread; handlers that touch the user interface must hand over to it.
    public event EventHandler? OutputArrived;

    public event EventHandler? TitleChanged;

    public event EventHandler? Ended;

    public event EventHandler? BellRang;

    public ITerminalOutputObserver? OutputObserver { get; set; }

    [SuppressMessage("Usage", "VSTHRD003", Justification = "The session completes on its own; nothing waits on the caller's context.")]
    public Task<int?> Exited => _session.Exited;

    public bool? IsInputHidden => _session.IsInputHidden;

    // After the session ended, e.g. when the connection to a host dropped, carries on in the same terminal with a new one, so
    // what it showed stays above. Modes a program left set, such as the alternate screen, are reset, since that program is
    // gone. Ended is raised again when the new session ends.
    [SuppressMessage("Usage", "VSTHRD003", Justification = "Both loops are ours; the reader has already finished when the session ended.")]
    public async Task ResumeAsync(ITerminalSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        await _lifetime.CancelAsync().ConfigureAwait(false);
        await Task.WhenAll(_reader, _writer).ConfigureAwait(false);
        await _session.DisposeAsync().ConfigureAwait(false);
        _lifetime.Dispose();
        _session = session;
        _input = NewInput();
        _lifetime = new CancellationTokenSource();
        Terminal.Feed("\e[?1049l\e[!p"u8);
        Terminal.ShellIntegrationNonce = session.ShellIntegrationNonce;
        LiveFromLine = Terminal.Primary.FirstVisibleLine + Terminal.Cursor.Row;
        _reader = Task.Run(() => ReadLoopAsync(_lifetime.Token), CancellationToken.None);
        _writer = Task.Run(() => WriteLoopAsync(_lifetime.Token), CancellationToken.None);
    }

    private static Channel<byte[]> NewInput() => Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });

    public void Send(ReadOnlySpan<byte> data) => _input.Writer.TryWrite(data.ToArray());

    public Task ResizeAsync(int columns, int rows, CancellationToken cancellationToken)
    {
        OutputObserver?.Resized(columns, rows);
        return _session.ResizeAsync(columns, rows, cancellationToken);
    }

    [SuppressMessage("Usage", "VSTHRD003", Justification = "Both loops are ours, started in the constructor on the thread pool.")]
    public async ValueTask DisposeAsync()
    {
        _input.Writer.TryComplete();
        await _lifetime.CancelAsync().ConfigureAwait(false);
        await _session.DisposeAsync().ConfigureAwait(false);
        await Task.WhenAll(_reader, _writer).ConfigureAwait(false);
        _lifetime.Dispose();
    }

    void ITerminalHost.Reply(ReadOnlySpan<byte> data) => Send(data);

    void ITerminalHost.Bell() => BellRang?.Invoke(this, EventArgs.Empty);

    void ITerminalHost.TitleChanged(string title) => TitleChanged?.Invoke(this, EventArgs.Empty);

    void ITerminalHost.WorkingDirectoryChanged(string path)
    {
        // Read from Terminal.WorkingDirectory when needed.
    }

    void ITerminalHost.ClipboardWriteRequested(string text)
    {
        // Writing the clipboard from a program (OSC 52) asks the user first; not wired yet.
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        try
        {
            int count;
            while ((count = await _session.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                OutputObserver?.Observe(buffer.AsSpan(0, count));
                Terminal.Feed(buffer.AsSpan(0, count));
                OutputArrived?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Closed by us.
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            // The session ended under us.
        }

        _input.Writer.TryComplete();
        Ended?.Invoke(this, EventArgs.Empty);
    }

    private async Task WriteLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var data in _input.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                await _session.WriteAsync(data, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Closed by us.
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            // The session ended; what was still queued can't be delivered.
        }
    }
}
