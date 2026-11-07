namespace Datapoint.Terminal.Agent.Hosts;

// The workspace's hosts/ folder: one folder per host, with README.md and the maintenance log in log/, one file per entry, so
// two people logging on the same host never change the same file (product decision 100). IsIgnored is the workspace's
// .terminalignore check; a read-only window can't write. Author names whoever runs this copy of the application in log entries.
public sealed record HostDocsFolder(string Path, Func<string, bool> IsIgnored, bool CanWrite)
{
    public const string DocumentName = "README.md";
    public const string LogFolderName = "log";

    public string Author { get; init; } = Environment.UserName;

    public string FolderFor(string connection) => System.IO.Path.Combine(Path, HostDocument.FolderNameFor(connection));

    public string FileFor(string connection) => System.IO.Path.Combine(FolderFor(connection), DocumentName);

    public string LogFor(string connection) => System.IO.Path.Combine(FolderFor(connection), LogFolderName);

    public IReadOnlyList<string> Documented()
    {
        try
        {
            return Directory.Exists(Path)
                ? [.. Directory.EnumerateDirectories(Path)
                    .Where(folder => File.Exists(System.IO.Path.Combine(folder, DocumentName)) || Directory.Exists(System.IO.Path.Combine(folder, LogFolderName)))
                    .Where(folder => !IsIgnored(System.IO.Path.Combine(folder, DocumentName)))
                    .Select(System.IO.Path.GetFileName)
                    .OfType<string>()
                    .Order(StringComparer.OrdinalIgnoreCase)]
                : [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
