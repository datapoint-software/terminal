namespace Datapoint.Terminal.Emulator;

public interface ITerminalHost
{
    void Reply(ReadOnlySpan<byte> data);

    void Bell();

    void TitleChanged(string title);

    void WorkingDirectoryChanged(string path);

    void ClipboardWriteRequested(string text);
}
