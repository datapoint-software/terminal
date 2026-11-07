namespace Datapoint.Terminal.Agent.Projects.Git;

// Reads single values from Git's configuration files as text. Includes and conditional includes aren't followed.
internal static class GitConfig
{
    private const int MaxFileBytes = 1024 * 1024;

    public static string? Read(string path, string section, string key)
    {
        string[] lines;
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaxFileBytes)
            {
                return null;
            }

            lines = File.ReadAllLines(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        string? current = null;
        string? value = null;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                current = line.TrimStart('[').Split([']', ' ', '"'], 2)[0];
                continue;
            }

            value = ValueOf(line, current, section, key) ?? value;
        }

        return value;
    }

    private static string? ValueOf(string line, string? current, string section, string key)
    {
        var equals = line.IndexOf('=', StringComparison.Ordinal);
        if (equals < 0 || !string.Equals(current, section, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(line[..equals].Trim(), key, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var value = line[(equals + 1)..].Trim();
        return value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;
    }
}
