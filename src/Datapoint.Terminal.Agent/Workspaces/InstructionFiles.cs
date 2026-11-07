using System.Text;

namespace Datapoint.Terminal.Agent.Workspaces;

// Product decision 100: the user's own file and the workspace's, nothing else. Files in parent folders, AGENTS.md and other
// tools' files aren't read, so a workspace means the same thing wherever its folder is kept.
public static class InstructionFiles
{
    public const string FileName = "TERMINAL.md";
    public const int MaxBytes = 32 * 1024;

    public static string? FindGlobal(string globalFolder)
    {
        var path = Path.Combine(globalFolder, FileName);
        return File.Exists(path) ? path : null;
    }

    // The global file, when given, isn't listed again (e.g. a link to it in the workspace).
    public static IReadOnlyList<string> FindForWorkspace(string workspaceFolder, string? globalFile)
    {
        var path = Path.Combine(WorkspaceFolders.Normalize(workspaceFolder), FileName);
        return !File.Exists(path) || (globalFile is not null && WorkspaceFolders.Comparer.Equals(RealPath(path), RealPath(globalFile)))
            ? []
            : [path];
    }

    // The cap is shared, in order: once it's reached, later files are cut short or left out, and say so.
    public static IReadOnlyList<InstructionFile> Read(IReadOnlyList<string> paths, int maxBytes = MaxBytes)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var files = new List<InstructionFile>();
        var left = maxBytes;
        foreach (var path in paths)
        {
            var file = ReadCapped(path, left);
            left -= Encoding.UTF8.GetByteCount(file.Text);
            files.Add(file);
        }

        return files;
    }

    private static string RealPath(string path)
    {
        try
        {
            return File.ResolveLinkTarget(path, returnFinalTarget: true)?.FullName ?? path;
        }
        catch (IOException)
        {
            return path;
        }
    }

    private static InstructionFile ReadCapped(string path, int left)
    {
        byte[] bytes;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            bytes = new byte[Math.Max(0, left) + 1];
            bytes = bytes[..stream.ReadAtLeast(bytes, bytes.Length, throwOnEndOfStream: false)];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new InstructionFile(path, string.Empty, IsCut: false, exception.Message);
        }

        var isCut = bytes.Length > left;
        var length = isCut ? Math.Max(0, left) : bytes.Length;

        // Never cut in the middle of a character.
        while (isCut && length > 0 && (bytes[length] & 0xC0) == 0x80)
        {
            length--;
        }

        var text = Encoding.UTF8.GetString(bytes, 0, length).TrimStart('﻿');
        return new InstructionFile(path, text, isCut);
    }
}
