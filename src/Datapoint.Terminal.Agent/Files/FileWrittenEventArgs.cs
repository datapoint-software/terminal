namespace Datapoint.Terminal.Agent.Files;

public sealed class FileWrittenEventArgs(string path, string text) : EventArgs
{
    public string Path { get; } = path;

    public string Text { get; } = text;
}
