using System.Text;
using Datapoint.Terminal.Security.Cryptography;
using Datapoint.Terminal.Sessions.Ssh.Config;
using Datapoint.Terminal.Sessions.Ssh.Keys;

namespace Datapoint.Terminal.Sessions.Ssh.KnownHosts;

// known_hosts as OpenSSH writes it: plain or hashed (|1|salt|hash) names, [host]:port for ports other than 22, patterns with
// wildcards and negation, and @revoked keys. @cert-authority lines are skipped until certificates are supported.
public sealed class KnownHostsFile
{
    private const int MaxFileBytes = 4 * 1024 * 1024;

    private readonly IReadOnlyList<string> _paths;

    public KnownHostsFile(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _paths = paths;
    }

    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh", "known_hosts");

    public static string NameFor(string host, int port) => port == 22 ? host : $"[{host}]:{port}";

    public KnownHostResult Check(string host, int port, SshPublicKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var name = NameFor(host, port);
        var entries = _paths.SelectMany(Read).ToList();

        // A revoked key is refused wherever its line is, even when another line lists it for this host.
        if (entries.FirstOrDefault(entry => entry.IsRevoked && key.Blob.AsSpan().SequenceEqual(entry.KeyBlob)) is { } revoked)
        {
            return new KnownHostResult(KnownHostStatus.Revoked, [], revoked.Source, revoked.Line);
        }

        var knownTypes = new List<string>();
        KnownHostResult? changed = null;
        foreach (var entry in entries.Where(entry => !entry.IsRevoked))
        {
            if (!entry.Matches(name))
            {
                continue;
            }

            if (key.Blob.AsSpan().SequenceEqual(entry.KeyBlob))
            {
                return new KnownHostResult(KnownHostStatus.Known, [entry.KeyType], entry.Source, entry.Line);
            }

            knownTypes.Add(entry.KeyType);
            if (string.Equals(entry.KeyType, key.KeyType, StringComparison.Ordinal))
            {
                changed ??= new KnownHostResult(KnownHostStatus.Changed, [entry.KeyType], entry.Source, entry.Line);
            }
        }

        return changed ?? new KnownHostResult(KnownHostStatus.Unknown, [.. knownTypes.Distinct(StringComparer.Ordinal)]);
    }

    // Appended to the first file under a lock, by writing a new copy and moving it into place, so neither a crash nor ssh
    // writing at the same time leaves a broken file. The name is hashed, as HashKnownHosts does, so the file doesn't list
    // the user's hosts.
    public void Add(string host, int port, SshPublicKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var path = _paths[0];
        var folder = Path.GetDirectoryName(path)!;
        if (!Directory.Exists(folder))
        {
            CreatePrivateFolder(folder);
        }

        using var fileLock = AcquireLock(path + ".lock");
        var existing = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        var separator = existing.Length == 0 || existing.EndsWith('\n') ? string.Empty : "\n";
        var line = $"{Hash(NameFor(host, port))} {key.KeyType} {Convert.ToBase64String(key.Blob)}\n";
        var temporary = Path.Combine(folder, $".known_hosts-{Guid.NewGuid():N}.tmp");
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = File.Exists(path) ? File.GetUnixFileMode(path) : UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        using (var stream = new FileStream(temporary, options))
        {
            stream.Write(Encoding.UTF8.GetBytes(existing + separator + line));
        }

        File.Move(temporary, path, overwrite: true);
    }

    internal static string Hash(string name)
    {
        var salt = new byte[20];
        RandomBytes.Fill(salt);
        return $"|1|{Convert.ToBase64String(salt)}|{Convert.ToBase64String(Hmac(salt, name))}";
    }

    internal static byte[] Hmac(ReadOnlySpan<byte> salt, string name)
    {
        using var hmac = Hashing.StartHmac(HashKind.Sha1, salt);
        return hmac.Append(Encoding.UTF8.GetBytes(name)).Finish();
    }

    private static IEnumerable<KnownHostEntry> Read(string path)
    {
        string[] lines;
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaxFileBytes)
            {
                return [];
            }

            lines = File.ReadAllLines(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }

        return lines.Select((text, index) => KnownHostEntry.Parse(text, path, index + 1)).OfType<KnownHostEntry>().ToList();
    }

    private static FileStream AcquireLock(string path)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
            }
            catch (IOException) when (attempt < 50)
            {
                Thread.Sleep(20);
            }
        }
    }

    private static void CreatePrivateFolder(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path);
            return;
        }

        Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    internal static bool IsPatternMatch(string name, IReadOnlyList<string> patterns) => SshPattern.IsListMatch(name, patterns);
}
