using System.Text.Json;

namespace Datapoint.Terminal.Agent.Workspaces.HostKeys;

// The host key this workspace last saw for each connection, in its personal data (product decisions 93 and 100). It never decides whether a key
// is trusted, which only the user's known_hosts does; it only notices that a machine may have been replaced since this
// workspace's documentation was written. A null path, as in a read-only window, remembers nothing.
public sealed class WorkspaceHostKeys(string? path)
{
    public const string FileName = "host-keys.json";

    private readonly Lock _sync = new();

    // The key last seen for the connection, as "type fingerprint", or null when this workspace hasn't seen one.
    public string? LastSeen(string connection)
    {
        ArgumentException.ThrowIfNullOrEmpty(connection);
        lock (_sync)
        {
            return Load().GetValueOrDefault(connection);
        }
    }

    public void Record(string connection, string seen)
    {
        ArgumentException.ThrowIfNullOrEmpty(connection);
        ArgumentException.ThrowIfNullOrEmpty(seen);
        if (path is null)
        {
            return;
        }

        lock (_sync)
        {
            var keys = Load();
            if (keys.TryGetValue(connection, out var before) && string.Equals(before, seen, StringComparison.Ordinal))
            {
                return;
            }

            keys[connection] = seen;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(keys, WorkspaceJsonContext.Default.DictionaryStringString));
            File.Move(temporary, path, overwrite: true);
        }
    }

    // An unreadable file counts as empty: the next connection records its key again, which at worst skips one question.
    private Dictionary<string, string> Load()
    {
        try
        {
            if (path is not null && File.Exists(path)
                && JsonSerializer.Deserialize(File.ReadAllText(path), WorkspaceJsonContext.Default.DictionaryStringString) is { } keys)
            {
                return new Dictionary<string, string>(keys, StringComparer.Ordinal);
            }
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
        }

        return new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
