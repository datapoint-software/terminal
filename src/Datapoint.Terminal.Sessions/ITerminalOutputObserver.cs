namespace Datapoint.Terminal.Sessions;

// Sees the session's output before the emulator does, on the connection's reading thread, and each resize.
public interface ITerminalOutputObserver
{
    void Observe(ReadOnlySpan<byte> data);

    void Resized(int columns, int rows);
}
