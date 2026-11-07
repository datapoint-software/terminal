using Datapoint.Terminal.Agent.Files;
using Datapoint.Terminal.Agent.Memory;

namespace Datapoint.Terminal.Agent.Tests.Memory;

public sealed class MemoryFileTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("memory-").FullName;

    private string PathIn => Path.Combine(_folder, ".terminal", MemoryFile.FileName);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void Starts_a_new_file_with_the_header()
    {
        Assert.Equal(WriteResult.Changed, MemoryFile.Add(PathIn, "Use doas, not sudo"));

        Assert.StartsWith(MemoryFile.Header, File.ReadAllText(PathIn), StringComparison.Ordinal);
        Assert.Equal(["Use doas, not sudo"], MemoryFile.Notes(PathIn), StringComparer.Ordinal);
    }

    [Fact]
    public void Replaces_a_note_in_place_and_deletes_it_when_emptied()
    {
        MemoryFile.Add(PathIn, "Use doas");
        MemoryFile.Add(PathIn, "Answer briefly");

        Assert.Equal(WriteResult.Changed, MemoryFile.Replace(PathIn, "Use doas", "Use doas, not sudo,\non OpenBSD"));
        Assert.Equal(["Use doas, not sudo, on OpenBSD", "Answer briefly"], MemoryFile.Notes(PathIn), StringComparer.Ordinal);

        Assert.Equal(WriteResult.Changed, MemoryFile.Replace(PathIn, "Answer briefly", "  "));
        Assert.Equal(["Use doas, not sudo, on OpenBSD"], MemoryFile.Notes(PathIn), StringComparer.Ordinal);
        Assert.Equal(WriteResult.Unchanged, MemoryFile.Replace(PathIn, "Gone", "Anything"));
    }

    [Fact]
    public void Does_not_repeat_a_note()
    {
        MemoryFile.Add(PathIn, "Use doas, not sudo");

        Assert.Equal(WriteResult.Unchanged, MemoryFile.Add(PathIn, "use DOAS, not sudo"));
        Assert.Single(MemoryFile.Notes(PathIn));
    }

    [Fact]
    public void Keeps_what_the_user_wrote_around_the_notes()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathIn)!);
        File.WriteAllText(PathIn, "My notes\n\n- Prefer tabs\nSome prose.\n");

        MemoryFile.Add(PathIn, "Use doas");
        MemoryFile.Remove(PathIn, "prefer tabs");

        Assert.Equal("My notes\n\nSome prose.\n- Use doas\n", File.ReadAllText(PathIn));
    }

    [Fact]
    public void Refuses_a_note_once_full()
    {
        var note = new string('x', MemoryFile.MaxNoteLength);
        var result = WriteResult.Changed;
        for (var index = 0; result == WriteResult.Changed; index++)
        {
            result = MemoryFile.Add(PathIn, $"{index} {note}");
        }

        Assert.Equal(WriteResult.Full, result);
        Assert.True(new FileInfo(PathIn).Length <= MemoryFile.MaxBytes);
    }

    [Fact]
    public void Reports_a_note_it_cannot_find()
    {
        MemoryFile.Add(PathIn, "Use doas");

        Assert.Equal(WriteResult.Unchanged, MemoryFile.Remove(PathIn, "Use sudo"));
    }

    [Fact]
    public void Clearing_keeps_the_header()
    {
        MemoryFile.Add(PathIn, "Use doas");

        MemoryFile.Clear(PathIn);

        Assert.Equal(MemoryFile.Header, File.ReadAllText(PathIn));
        Assert.Empty(MemoryFile.Notes(PathIn));
    }

    [Fact]
    public void Keeps_the_file_private()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        MemoryFile.Add(PathIn, "Use doas");

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(PathIn));
    }
}
