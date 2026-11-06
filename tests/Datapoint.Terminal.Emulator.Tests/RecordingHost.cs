using System.Text;

namespace Datapoint.Terminal.Emulator.Tests;

public sealed class RecordingHost : ITerminalHost
{
    private readonly StringBuilder _replies = new();
    private readonly List<string> _clipboard = [];

    public string Replies => _replies.ToString();

    public IReadOnlyList<string> Clipboard => _clipboard;

    public int Bells { get; private set; }

    public void Reply(ReadOnlySpan<byte> data) => _replies.Append(Encoding.UTF8.GetString(data));

    public void Bell() => Bells++;

    public void TitleChanged(string title)
    {
    }

    public void WorkingDirectoryChanged(string path)
    {
    }

    public void ClipboardWriteRequested(string text) => _clipboard.Add(text);
}
