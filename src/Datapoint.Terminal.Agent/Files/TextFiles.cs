using System.Text;

namespace Datapoint.Terminal.Agent.Files;

public static class TextFiles
{
    public static string Read(string path) => File.Exists(path) ? File.ReadAllText(path) : string.Empty;

    // Written to a temporary file, then moved into place, so a crash never leaves half a file. A private file is readable only by
    // the user from the moment it's created. The temporary file is kept out of the file's folder where it can be (TemporaryFiles).
    public static void Write(string path, string text, bool isPrivate)
    {
        ArgumentNullException.ThrowIfNull(text);
        var folder = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(folder);
        var temporary = TemporaryFiles.For(path);
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (isPrivate && !OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        try
        {
            using (var stream = new FileStream(temporary, options))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                writer.Write(text);
            }

            File.Move(temporary, path, overwrite: true);
            FileWrites.Notify(path, text);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
