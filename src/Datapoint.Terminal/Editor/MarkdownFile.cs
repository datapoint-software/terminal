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

    private readonly bool _splitsHeader;

    private MarkdownFile(string path, bool splitsHeader)
    {
        Path = path;
        _splitsHeader = splitsHeader;
    }

    public string Path { get; }

    // The file as last read or saved, with "\n" line breaks; empty for a file that doesn't exist yet. For a file opened with
    // its header split off, the text after the header.
    public string Text { get; private set; } = string.Empty;

    // A skill's YAML header, from its first "---" line to the blank lines after its closing one; edited apart from the text.
    public string Header { get; private set; } = string.Empty;

    public bool Exists => _disk is not null;

    // Null when the file can't be edited here, with the reason in problem.
    public static MarkdownFile? Open(string path, out string? problem, bool splitsHeader = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var file = new MarkdownFile(path, splitsHeader);
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

        return Split(Normalize(disk ?? string.Empty)).Text;
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

        var whole = Header + text;
        var output = string.Equals(_newline, "\n", StringComparison.Ordinal) ? whole : whole.Replace("\n", _newline, StringComparison.Ordinal);
        var bytes = _hasBom ? [.. s_bom, .. s_strict.GetBytes(output)] : s_strict.GetBytes(output);
        Write(bytes);
        _disk = output;
        Text = text;
        return SaveResult.Saved;
    }

    // A new header in front of the text as last saved.
    public SaveResult SaveHeader(string header, bool overwrite = false)
    {
        ArgumentNullException.ThrowIfNull(header);
        if (!overwrite && ChangedOnDisk() is not null)
        {
            return SaveResult.Conflict;
        }

        var previous = Header;
        Header = header;
        try
        {
            return Save(Text, overwrite: true);
        }
        catch
        {
            Header = previous;
            throw;
        }
    }

    // The header and the text after it; no header unless asked for and the text starts with one.
    public (string Header, string Text) Split(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!_splitsHeader || !text.StartsWith("---\n", StringComparison.Ordinal))
        {
            return (string.Empty, text);
        }

        var close = text.IndexOf("\n---\n", 3, StringComparison.Ordinal);
        if (close < 0)
        {
            return text.EndsWith("\n---", StringComparison.Ordinal) ? (text + "\n", string.Empty) : (string.Empty, text);
        }

        var end = close + "\n---\n".Length;
        while (end < text.Length && text[end] == '\n')
        {
            end++;
        }

        return (text[..end], text[end..]);
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
        (Header, Text) = Split(Normalize(disk ?? string.Empty));
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);
}
