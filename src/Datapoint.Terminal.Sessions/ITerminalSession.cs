namespace Datapoint.Terminal.Sessions;

/// <summary>A shell the terminal talks to: local, through a pseudo-terminal, or remote, through an SSH channel.</summary>
public interface ITerminalSession : IAsyncDisposable
{
    /// <summary>The secret the shell integration script signs command lines with, or null when the shell has none.</summary>
    string? ShellIntegrationNonce { get; }

    /// <summary>Completes with the shell's exit code (null when unknown) once it has ended.</summary>
    Task<int?> Exited { get; }

    /// <summary>Reads output into <paramref name="buffer"/>; returns 0 once the shell has ended.</summary>
    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken);

    Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken);

    Task ResizeAsync(int columns, int rows, CancellationToken cancellationToken);
}
