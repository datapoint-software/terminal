namespace Datapoint.Terminal.Agent.Files;

// IsWritable is for the file, or for its folder when it doesn't exist yet; when false, writing it takes sudo.
public sealed record HostFile(bool Exists, byte[] Content, bool IsWritable, bool IsTruncated)
{
    public static HostFile Missing(bool isWritable) => new(Exists: false, [], isWritable, IsTruncated: false);
}
