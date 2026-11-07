using System.Buffers.Binary;
using System.Text;

namespace Datapoint.Terminal.Agent.Projects.Git;

// Reads the paths in Git's index (versions 2 to 4, as gitformat-index(5) describes them) to tell whether files are tracked.
internal static class GitIndex
{
    private const long MaxFileBytes = 256L * 1024 * 1024;

    // Null when the index is missing or can't be read.
    public static bool? HasEntriesUnder(string indexPath, string prefix, int hashLength, StringComparison comparison)
    {
        byte[] data;
        try
        {
            var info = new FileInfo(indexPath);
            if (!info.Exists || info.Length > MaxFileBytes)
            {
                return null;
            }

            data = File.ReadAllBytes(indexPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        try
        {
            return Paths(data, hashLength).Any(path => path.StartsWith(prefix, comparison));
        }
        catch (Exception exception) when (exception is ArgumentException or IndexOutOfRangeException or InvalidDataException)
        {
            return null;
        }
    }

    private static List<string> Paths(byte[] data, int hashLength)
    {
        if (data.Length < 12 || !data.AsSpan(0, 4).SequenceEqual("DIRC"u8))
        {
            throw new InvalidDataException("Not a Git index.");
        }

        var version = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(4));
        var count = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(8));
        if (version is < 2 or > 4)
        {
            throw new InvalidDataException("Unknown index version.");
        }

        var paths = new List<string>();
        var position = 12;
        var previous = string.Empty;
        for (var index = 0u; index < count; index++)
        {
            var start = position;
            position += 40 + hashLength;
            var flags = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(position));
            position += 2 + (version >= 3 && (flags & 0x4000) != 0 ? 2 : 0);
            var path = version == 4 ? ReadCompressed(data, ref position, previous) : ReadPadded(data, ref position, start);
            paths.Add(path);
            previous = path;
        }

        return paths;
    }

    // Each entry is padded with 1 to 8 NUL bytes to a multiple of eight.
    private static string ReadPadded(byte[] data, ref int position, int start)
    {
        var end = Array.IndexOf(data, (byte)0, position);
        if (end < 0)
        {
            throw new InvalidDataException("Unterminated path.");
        }

        var path = Encoding.UTF8.GetString(data, position, end - position);
        position = start + ((end - start + 8) & ~7);
        return path;
    }

    // Version 4 keeps only what differs from the previous path: how many bytes to drop from its end, then the rest.
    private static string ReadCompressed(byte[] data, ref int position, string previous)
    {
        var strip = ReadOffset(data, ref position);
        var end = Array.IndexOf(data, (byte)0, position);
        if (end < 0)
        {
            throw new InvalidDataException("Unterminated path.");
        }

        var previousBytes = Encoding.UTF8.GetBytes(previous);
        if (strip > previousBytes.Length)
        {
            throw new InvalidDataException("Bad path compression.");
        }

        var path = Encoding.UTF8.GetString([.. previousBytes.AsSpan(0, previousBytes.Length - (int)strip), .. data.AsSpan(position, end - position)]);
        position = end + 1;
        return path;
    }

    private static long ReadOffset(byte[] data, ref int position)
    {
        var current = data[position++];
        long value = current & 0x7F;
        while ((current & 0x80) != 0 && value < int.MaxValue)
        {
            current = data[position++];
            value = ((value + 1) << 7) | (current & 0x7Fu);
        }

        return value;
    }
}
