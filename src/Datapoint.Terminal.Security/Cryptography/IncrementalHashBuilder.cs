using System.Security.Cryptography;

namespace Datapoint.Terminal.Security.Cryptography;

public sealed class IncrementalHashBuilder(IncrementalHash hash) : IDisposable
{
    public int HashSize => hash.HashLengthInBytes;

    public IncrementalHashBuilder Append(ReadOnlySpan<byte> data)
    {
        hash.AppendData(data);
        return this;
    }

    public byte[] Finish() => hash.GetHashAndReset();

    public int Finish(Span<byte> destination) => hash.GetHashAndReset(destination);

    public void Dispose() => hash.Dispose();
}
