namespace Datapoint.Terminal.Emulator;

public interface ITerminalHost
{
    void Reply(ReadOnlySpan<byte> data);

    void Bell();

    void TitleChanged(string title);

    void WorkingDirectoryChanged(string uri);

    void ClipboardWriteRequested(string text);
}
