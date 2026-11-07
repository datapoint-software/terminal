namespace Datapoint.Terminal.Agent.Files;

// The computer the application runs on, for local terminals.
public sealed class LocalHostFiles(string home) : IHostFiles
{
    public async Task<HostFile> ReadAsync(string path, int maxBytes, CancellationToken cancellationToken)
    {
        var full = Expand(path);
        if (Directory.Exists(full))
        {
            throw new IOException("It's a folder, not a file.");
        }

        if (!File.Exists(full))
        {
            var folder = Path.GetDirectoryName(full);
            return Directory.Exists(folder) ? HostFile.Missing(CanCreateIn(folder)) : throw new IOException("Its folder doesn't exist.");
        }

        var stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, useAsync: true);
        await using var disposing = stream.ConfigureAwait(false);
        var buffer = new byte[Math.Min(stream.Length, (long)maxBytes + 1)];
        var read = await stream.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
        var isTruncated = read > maxBytes;
        return new HostFile(Exists: true, buffer[..Math.Min(read, maxBytes)], CanWrite(full), isTruncated);
    }

    public async Task<string> StageAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        var folder = Expand(HostFolders.Staging);
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(folder);
        }
        else
        {
            Directory.CreateDirectory(folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        var name = Guid.NewGuid().ToString("N");
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Options = FileOptions.Asynchronous };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        var stream = new FileStream(Path.Combine(folder, name), options);
        await using (stream.ConfigureAwait(false))
        {
            await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
        }

        return HostFolders.Staging + "/" + name;
    }

    private string Expand(string path) => path.StartsWith("~/", StringComparison.Ordinal) ? Path.Combine(home, path[2..]) : path;

    // Opening for writing without truncating checks the permission the way the shell's redirection would.
    private static bool CanWrite(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    private static bool CanCreateIn(string folder)
    {
        try
        {
            using var probe = new FileStream(Path.Combine(folder, ".datapoint-" + Guid.NewGuid().ToString("N")), FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }
}
