using System.Text;

namespace Datapoint.Terminal.Editor;

// A Markdown file on disk, as the editor reads and writes it. Line breaks are "\n" while editing and go back to the file's own
// on saving, with its byte order mark if it had one. Nothing is written over a change made since the file was read, e.g. by
// the agent: the save reports a conflict instead.
public sealed class MarkdownFile
{
    public const int MaxBytes = 2 * 1024 * 1024;

    private static readonly UTF8Encoding s_strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly byte[] s_bom = [0xEF, 0xBB, 0xBF];

    private string? _disk;
    private bool _hasBom;
    private string _newline = "\n";

    private MarkdownFile(string path) => Path = path;

    public string Path { get; }

    // The file as last read or saved, with "\n" line breaks; empty for a file that doesn't exist yet.
    public string Text { get; private set; } = string.Empty;

    public bool Exists => _disk is not null;

    // Null when the file can't be edited here, with the reason in problem.
    public static MarkdownFile? Open(string path, out string? problem)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var file = new MarkdownFile(path);
        problem = file.Read(out var disk, out var hasBom);
        if (problem is not null)
        {
            return null;
        }

        file.Accept(disk, hasBom);
        return file;
    }

    // What's on disk now, if it differs from what the editor last read or saved.
    public string? ChangedOnDisk()
    {
        if (Read(out var disk, out _) is not null || string.Equals(disk, _disk, StringComparison.Ordinal))
        {
            return null;
        }

        return Normalize(disk ?? string.Empty);
    }

    // Takes what's on disk now, dropping what the editor had.
    public void Reload()
    {
        if (Read(out var disk, out var hasBom) is not null)
        {
            return;
        }

        Accept(disk, hasBom);
    }

    public SaveResult Save(string text, bool overwrite = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!overwrite && ChangedOnDisk() is not null)
        {
            return SaveResult.Conflict;
        }

        var output = string.Equals(_newline, "\n", StringComparison.Ordinal) ? text : text.Replace("\n", _newline, StringComparison.Ordinal);
        var bytes = _hasBom ? [.. s_bom, .. s_strict.GetBytes(output)] : s_strict.GetBytes(output);
        Write(bytes);
        _disk = output;
        Text = text;
        return SaveResult.Saved;
    }

    // A temporary file then a rename, so a crash can't leave half a document; a link is written through, so it stays a link.
    private void Write(byte[] bytes)
    {
        var info = new FileInfo(Path);
        if (info.LinkTarget is not null)
        {
            File.WriteAllBytes(Path, bytes);
            return;
        }

        var folder = System.IO.Path.GetDirectoryName(Path)!;
        Directory.CreateDirectory(folder);
        var temporary = System.IO.Path.Combine(folder, "." + System.IO.Path.GetFileName(Path) + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp");
        try
        {
            File.WriteAllBytes(temporary, bytes);
            if (info.Exists && !OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(temporary, File.GetUnixFileMode(Path));
            }

            File.Move(temporary, Path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private string? Read(out string? disk, out bool hasBom)
    {
        disk = null;
        hasBom = false;
        if (!File.Exists(Path))
        {
            return Directory.Exists(Path) ? "it's a folder." : null;
        }

        try
        {
            if (new FileInfo(Path).Length > MaxBytes)
            {
                return "it's larger than 2 MB.";
            }

            var bytes = File.ReadAllBytes(Path);
            hasBom = bytes.AsSpan().StartsWith(s_bom);
            disk = s_strict.GetString(bytes, hasBom ? 3 : 0, bytes.Length - (hasBom ? 3 : 0));
            return disk.Contains('\0', StringComparison.Ordinal) ? "it isn't text." : null;
        }
        catch (DecoderFallbackException)
        {
            return "it isn't UTF-8 text.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return exception.Message;
        }
    }

    private void Accept(string? disk, bool hasBom)
    {
        _disk = disk;
        _hasBom = hasBom;
        _newline = disk?.Contains("\r\n", StringComparison.Ordinal) == true ? "\r\n" : "\n";
        Text = Normalize(disk ?? string.Empty);
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);
}
