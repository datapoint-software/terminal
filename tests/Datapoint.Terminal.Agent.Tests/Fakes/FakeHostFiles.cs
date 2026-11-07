using System.Text;
using Datapoint.Terminal.Agent.Files;

namespace Datapoint.Terminal.Agent.Tests.Fakes;

internal sealed class FakeHostFiles : IHostFiles
{
    public Dictionary<string, string> Files { get; } = new(StringComparer.Ordinal);

    public HashSet<string> ReadOnly { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> Staged { get; } = new(StringComparer.Ordinal);

    public List<string> Reads { get; } = [];

    // Runs before each read after the first, e.g. to change the file while an edit waits.
    public Action? BeforeNextRead { get; set; }

    public Task<HostFile> ReadAsync(string path, int maxBytes, CancellationToken cancellationToken)
    {
        if (Reads.Count > 0)
        {
            BeforeNextRead?.Invoke();
        }

        Reads.Add(path);
        var writable = !ReadOnly.Contains(path);
        return Task.FromResult(Files.TryGetValue(path, out var text)
            ? new HostFile(Exists: true, Encoding.UTF8.GetBytes(text), writable, IsTruncated: false)
            : HostFile.Missing(writable));
    }

    public Task<string> StageAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        var path = $"~/.datapoint-terminal/staging/{Staged.Count + 1}";
        Staged[path] = Encoding.UTF8.GetString(content.Span);
        return Task.FromResult(path);
    }
}
