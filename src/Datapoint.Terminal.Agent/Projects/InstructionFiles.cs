using System.Text;

namespace Datapoint.Terminal.Agent.Projects;

// Product decision 87. Stopping at the home folder keeps files that others left in shared folders (e.g. /tmp) out.
public static class InstructionFiles
{
    public const string FileName = "TERMINAL.md";
    public const string FallbackName = "AGENTS.md";
    public const int MaxBytes = 32 * 1024;

    public static string? FindGlobal(string globalFolder)
    {
        var path = Path.Combine(globalFolder, FileName);
        return File.Exists(path) ? path : null;
    }

    // Broadest folder first, so nearer files come later and take precedence. The global file, when given, isn't listed again.
    public static IReadOnlyList<string> FindForProject(string projectFolder, string home, string? globalFile)
    {
        var seen = new HashSet<string>(ProjectFolders.Comparer);
        if (globalFile is not null)
        {
            seen.Add(RealPath(globalFile));
        }

        var files = new List<string>();
        foreach (var folder in FoldersFor(projectFolder, home))
        {
            var file = InFolder(folder);
            if (file is not null && seen.Add(RealPath(file)))
            {
                files.Add(file);
            }
        }

        return files;
    }

    public static IReadOnlyList<string> FoldersFor(string projectFolder, string home)
    {
        var project = ProjectFolders.Normalize(projectFolder);
        var root = ProjectFolders.Normalize(home);
        var folders = new List<string>();
        for (var folder = project; folder is not null; folder = Path.GetDirectoryName(folder))
        {
            folders.Add(folder);
            if (ProjectFolders.Comparer.Equals(folder, root))
            {
                folders.Reverse();
                return folders;
            }
        }

        return [project];
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

    private static string? InFolder(string folder)
    {
        var preferred = Path.Combine(folder, FileName);
        if (File.Exists(preferred))
        {
            return preferred;
        }

        var fallback = Path.Combine(folder, FallbackName);
        return File.Exists(fallback) ? fallback : null;
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
