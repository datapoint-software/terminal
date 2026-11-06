namespace Datapoint.Terminal.Sessions;

public interface ITerminalSession : IAsyncDisposable
{
    string? ShellIntegrationNonce { get; }

    Task<int?> Exited { get; }

    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken);

    Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken);

    Task ResizeAsync(int columns, int rows, CancellationToken cancellationToken);
}
