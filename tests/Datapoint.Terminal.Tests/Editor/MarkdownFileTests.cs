using System.Text;
using Datapoint.Terminal.Editor;

namespace Datapoint.Terminal.Tests.Editor;

public sealed class MarkdownFileTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "markdown-file-" + Guid.NewGuid().ToString("N"));

    public MarkdownFileTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void Keeps_the_files_line_breaks_and_byte_order_mark()
    {
        var path = Path.Combine(_folder, "notes.md");
        File.WriteAllBytes(path, [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("# Notes\r\n\r\nOne\r\n")]);

        var file = MarkdownFile.Open(path, out var problem)!;
        Assert.Null(problem);
        Assert.Equal("# Notes\n\nOne\n", file.Text);

        Assert.Equal(SaveResult.Saved, file.Save("# Notes\n\nOne\nTwo\n"));
        Assert.Equal<byte>([0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("# Notes\r\n\r\nOne\r\nTwo\r\n")], File.ReadAllBytes(path));
    }

    [Fact]
    public void Reports_a_change_made_elsewhere_instead_of_writing_over_it()
    {
        var path = Path.Combine(_folder, "web1.md");
        File.WriteAllText(path, "# web1\n");
        var file = MarkdownFile.Open(path, out _)!;

        File.WriteAllText(path, "# web1\n\nUpdated by the agent.\n");

        Assert.Equal("# web1\n\nUpdated by the agent.\n", file.ChangedOnDisk());
        Assert.Equal(SaveResult.Conflict, file.Save("# web1\n\nMine.\n"));
        Assert.Equal("# web1\n\nUpdated by the agent.\n", File.ReadAllText(path));
        Assert.Equal(SaveResult.Saved, file.Save("# web1\n\nMine.\n", overwrite: true));
        Assert.Null(file.ChangedOnDisk());
    }

    [Fact]
    public void Creates_a_missing_file_and_its_folder_on_the_first_save()
    {
        var path = Path.Combine(_folder, "hosts", "db1.md");
        var file = MarkdownFile.Open(path, out var problem)!;
        Assert.Null(problem);
        Assert.False(file.Exists);
        Assert.Equal(string.Empty, file.Text);

        file.Save("# db1\n");

        Assert.Equal("# db1\n", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(Path.Combine(_folder, "hosts"), "*.tmp"));
    }

    [Fact]
    public void Refuses_what_it_cant_edit()
    {
        var binary = Path.Combine(_folder, "image.md");
        File.WriteAllBytes(binary, [0x89, 0x50, 0x00, 0x47]);
        var latin = Path.Combine(_folder, "latin.md");
        File.WriteAllBytes(latin, [0x63, 0x61, 0x66, 0xE9]);

        Assert.Null(MarkdownFile.Open(binary, out var binaryProblem));
        Assert.Null(MarkdownFile.Open(latin, out var latinProblem));
        Assert.Null(MarkdownFile.Open(_folder, out var folderProblem));
        Assert.NotNull(binaryProblem);
        Assert.NotNull(latinProblem);
        Assert.NotNull(folderProblem);
    }

    [Fact]
    public void Keeps_the_files_permissions()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var path = Path.Combine(_folder, "private.md");
        File.WriteAllText(path, "secret\n");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        MarkdownFile.Open(path, out _)!.Save("still secret\n");

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
    }
}
