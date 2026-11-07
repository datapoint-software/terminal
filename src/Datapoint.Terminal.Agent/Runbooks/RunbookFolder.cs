namespace Datapoint.Terminal.Agent.Runbooks;

// The project's runbooks/ folder. In a project the user hasn't trusted, runbooks are shown to the model as data, not steps to
// follow. IsTrusted is asked each time, since trust can change while the window is open.
public sealed record RunbookFolder(string Path, Func<string, bool> IsIgnored, bool CanWrite, Func<bool> IsTrusted)
{
    public string FileFor(string name) => System.IO.Path.Combine(Path, RunbookDocument.FileNameFor(name));

    public IReadOnlyList<string> Names()
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
