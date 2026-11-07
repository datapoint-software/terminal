namespace Datapoint.Terminal.Agent.Hosts;

// The workspace's hosts/ folder. IsIgnored is the workspace's .terminalignore check; a read-only window can't write.
public sealed record HostDocsFolder(string Path, Func<string, bool> IsIgnored, bool CanWrite)
{
    public string FileFor(string connection) => System.IO.Path.Combine(Path, HostDocument.FileNameFor(connection));

    public IReadOnlyList<string> Documented()
    {
        try
        {
            return Directory.Exists(Path)
                ? [.. Directory.EnumerateFiles(Path, "*.md").Where(file => !IsIgnored(file)).Select(System.IO.Path.GetFileNameWithoutExtension).OfType<string>().Order(StringComparer.OrdinalIgnoreCase)]
                : [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
