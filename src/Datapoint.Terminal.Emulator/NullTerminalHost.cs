namespace Datapoint.Terminal.Emulator;

public sealed class NullTerminalHost : ITerminalHost
{
    public static NullTerminalHost Instance { get; } = new();

    public void Reply(ReadOnlySpan<byte> data)
    {
    }

    public void Bell()
    {
    }

    public void TitleChanged(string title)
    {
    }

    public void WorkingDirectoryChanged(string path)
    {
    }

    public void ClipboardWriteRequested(string text)
    {
    }
}
