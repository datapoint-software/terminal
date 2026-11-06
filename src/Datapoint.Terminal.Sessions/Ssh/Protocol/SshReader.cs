using System.Buffers.Binary;
using System.Text;

namespace Datapoint.Terminal.Sessions.Ssh.Protocol;

public ref struct SshReader(ReadOnlySpan<byte> data)
{
    public const int MaxNameListLength = 64 * 1024;
    public const int MaxNames = 256;

    private readonly ReadOnlySpan<byte> _data = data;
    private int _position;

    public readonly int Remaining => _data.Length - _position;

    public readonly bool IsAtEnd => _position == _data.Length;

    public byte ReadByte()
    {
        var value = Take(1)[0];
        return value;
    }

    public bool ReadBoolean() => ReadByte() != 0;

    public uint ReadUInt32() => BinaryPrimitives.ReadUInt32BigEndian(Take(4));

    public ulong ReadUInt64() => BinaryPrimitives.ReadUInt64BigEndian(Take(8));

    public ReadOnlySpan<byte> ReadString()
    {
        var length = ReadUInt32();
        if (length > (uint)Remaining)
        {
            throw new SshProtocolException("A string is longer than its message.");
        }

        return Take((int)length);
    }

    public string ReadUtf8String() => Encoding.UTF8.GetString(ReadString());

    public string ReadAsciiString()
    {
        var bytes = ReadString();
        foreach (var value in bytes)
        {
            if (value is < 0x20 or > 0x7E)
            {
                throw new SshProtocolException("A name contains characters outside printable ASCII.");
            }
        }

        return Encoding.ASCII.GetString(bytes);
    }

    public IReadOnlyList<string> ReadNameList()
    {
        var text = ReadAsciiString();
        if (text.Length > MaxNameListLength)
        {
            throw new SshProtocolException("A name-list is too long.");
        }

        var names = text.Length == 0 ? [] : text.Split(',');
        if (names.Length > MaxNames || names.Any(name => name.Length == 0))
        {
            throw new SshProtocolException("A name-list is malformed.");
        }

        return names;
    }

    public ReadOnlySpan<byte> ReadMPIntUnsigned()
    {
        var value = ReadString();
        if (!value.IsEmpty && (value[0] & 0x80) != 0)
        {
            throw new SshProtocolException("A positive mpint was expected.");
        }

        return value.TrimStart((byte)0);
    }

    public ReadOnlySpan<byte> ReadRest() => Take(Remaining);

    public ReadOnlySpan<byte> ReadRaw(int count) => Take(count);

    public readonly void ExpectEnd()
    {
        if (IsAtEnd)
        {
            return;
        }

        throw new SshProtocolException("A message has unexpected trailing data.");
    }

    private ReadOnlySpan<byte> Take(int count)
    {
        if (count > Remaining)
        {
            throw new SshProtocolException("A message ended early.");
        }

        var slice = _data.Slice(_position, count);
        _position += count;
        return slice;
    }
}
