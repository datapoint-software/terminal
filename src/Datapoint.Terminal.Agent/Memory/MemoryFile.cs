using System.Text;

namespace Datapoint.Terminal.Agent.Memory;

// A MEMORY.md file (product decisions 29 and 88): a header the user can read, then one note per "- " line. The user may edit
// it freely; lines that aren't notes are kept as they are.
public static class MemoryFile
{
    public const string FileName = "MEMORY.md";

    // Memory is meant to be short; past this the agent is asked to forget something first.
    public const int MaxBytes = 8 * 1024;

    public const int MaxNoteLength = 300;

    private const string Bullet = "- ";

    private static readonly Lock s_sync = new();

    public static string Header => """
        # Memory

        Notes the agent in Datapoint Terminal keeps about your preferences, one per line. Edit or delete them freely; your
        instruction files take precedence over them.

        """;

    public static IReadOnlyList<string> Notes(string path) => [.. NotesIn(ReadText(path))];

    public static MemoryResult Add(string path, string note)
    {
        ArgumentNullException.ThrowIfNull(note);
        lock (s_sync)
        {
            var text = ReadText(path);
            if (NotesIn(text).Contains(note, StringComparer.OrdinalIgnoreCase))
            {
                return MemoryResult.Unchanged;
            }

            var updated = (text.Length == 0 ? Header : text.TrimEnd('\n', '\r') + "\n") + Bullet + note + "\n";
            if (Encoding.UTF8.GetByteCount(updated) > MaxBytes)
            {
                return MemoryResult.Full;
            }

            Write(path, updated);
            return MemoryResult.Changed;
        }
    }

    public static MemoryResult Remove(string path, string note)
    {
        ArgumentNullException.ThrowIfNull(note);
        lock (s_sync)
        {
            var lines = ReadText(path).Split('\n').ToList();
            var index = lines.FindIndex(line => IsNote(line.TrimEnd('\r'))
                && string.Equals(line.TrimEnd('\r')[Bullet.Length..].Trim(), note, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                return MemoryResult.Unchanged;
            }

            lines.RemoveAt(index);
            Write(path, string.Join('\n', lines));
            return MemoryResult.Changed;
        }
    }

    // Leaves the header, so the file still explains itself.
    public static void Clear(string path)
    {
        lock (s_sync)
        {
            Write(path, Header);
        }
    }

    public static void Create(string path)
    {
        lock (s_sync)
        {
            if (File.Exists(path))
            {
                return;
            }

            Write(path, Header);
        }
    }

    private static IEnumerable<string> NotesIn(string text) =>
        text.Split('\n').Select(line => line.TrimEnd('\r')).Where(IsNote).Select(line => line[Bullet.Length..].Trim());

    private static bool IsNote(string line) => line.StartsWith(Bullet, StringComparison.Ordinal) && line.Length > Bullet.Length;

    private static string ReadText(string path) => File.Exists(path) ? File.ReadAllText(path) : string.Empty;

    // Written to a private temporary file, then moved into place, so a crash never leaves half a file.
    private static void Write(string path, string text)
    {
        var folder = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(folder);
        var temporary = Path.Combine(folder, $".memory-{Guid.NewGuid():N}.tmp");
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        using (var stream = new FileStream(temporary, options))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            writer.Write(text);
        }

        File.Move(temporary, path, overwrite: true);
    }
}
