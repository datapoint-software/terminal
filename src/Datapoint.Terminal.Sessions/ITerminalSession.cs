namespace Datapoint.Terminal.Sessions;

public interface ITerminalSession : IAsyncDisposable
{
    string? ShellIntegrationNonce { get; }

    // True while a program waits for input it doesn't show, such as a password; null where that can't be known, e.g. over SSH,
    // whose protocol doesn't report the host's terminal modes.
    bool? IsInputHidden => null;

    Task<int?> Exited { get; }

    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken);

    Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken);

    Task ResizeAsync(int columns, int rows, CancellationToken cancellationToken);
}
