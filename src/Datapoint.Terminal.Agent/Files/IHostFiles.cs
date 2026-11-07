namespace Datapoint.Terminal.Agent.Files;

// Exact file access on the machine a terminal's shell runs on, beside the terminal rather than typed into it (product decision
// 57). Paths are absolute or start with ~/. Every use is shown in the chat (decision 58).
public interface IHostFiles
{
    // Throws IOException when the file exists but can't be read, or isn't a regular file.
    Task<HostFile> ReadAsync(string path, int maxBytes, CancellationToken cancellationToken);

    // Saves content to a new file only the user can read and returns its path as a shell names it, starting with ~/.
    Task<string> StageAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken);
}
