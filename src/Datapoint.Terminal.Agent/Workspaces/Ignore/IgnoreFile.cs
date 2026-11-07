namespace Datapoint.Terminal.Agent.Workspaces.Ignore;

// The patterns of one ignore file, which apply to paths inside its folder. Later patterns take precedence.
public sealed class IgnoreFile
{
    private const int MaxFileBytes = 1024 * 1024;

    private readonly IReadOnlyList<IgnorePattern> _patterns;

    public IgnoreFile(string folder, IEnumerable<string> lines, bool ignoreCase)
    {
        ArgumentNullException.ThrowIfNull(lines);
        Folder = WorkspaceFolders.Normalize(folder);
        _patterns = [.. lines.Select(line => IgnorePattern.Parse(line, ignoreCase)).OfType<IgnorePattern>()];
    }

    public string Folder { get; }

    public static IgnoreFile? Load(string path, string folder, bool ignoreCase)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists && info.Length <= MaxFileBytes ? new IgnoreFile(folder, File.ReadAllLines(path), ignoreCase) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // Null when no pattern speaks about the path; otherwise whether the last matching pattern ignores it.
    public bool? Decide(string path, bool isDirectory)
    {
        var relative = Relative(path);
        if (relative is null)
        {
            return null;
        }

        for (var index = _patterns.Count - 1; index >= 0; index--)
        {
            if (_patterns[index].Matches(relative, isDirectory))
            {
                return !_patterns[index].IsNegated;
            }
        }

        return null;
    }

    private string? Relative(string path) => WorkspaceFolders.RelativeInside(Folder, path);
}
