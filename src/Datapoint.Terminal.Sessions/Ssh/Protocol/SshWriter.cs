using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace Datapoint.Terminal.Sessions.Ssh.Protocol;

public sealed class SshWriter
{
    private readonly ArrayBufferWriter<byte> _buffer = new(256);

    public int Length => _buffer.WrittenCount;

    public ReadOnlySpan<byte> WrittenSpan => _buffer.WrittenSpan;

    public byte[] ToArray() => _buffer.WrittenSpan.ToArray();

    public SshWriter WriteByte(byte value)
    {
        _buffer.GetSpan(1)[0] = value;
        _buffer.Advance(1);
        return this;
    }

    public SshWriter WriteBoolean(bool value) => WriteByte(value ? (byte)1 : (byte)0);

    public SshWriter WriteUInt32(uint value)
    {
        BinaryPrimitives.WriteUInt32BigEndian(_buffer.GetSpan(4), value);
        _buffer.Advance(4);
        return this;
    }

    public SshWriter WriteUInt64(ulong value)
    {
        BinaryPrimitives.WriteUInt64BigEndian(_buffer.GetSpan(8), value);
        _buffer.Advance(8);
        return this;
    }

    public SshWriter WriteRaw(ReadOnlySpan<byte> data)
    {
        _buffer.Write(data);
        return this;
    }

    public SshWriter WriteString(ReadOnlySpan<byte> data)
    {
        WriteUInt32((uint)data.Length);
        return WriteRaw(data);
    }

    public SshWriter WriteString(string text) => WriteString(Encoding.UTF8.GetBytes(text));

    public SshWriter WriteNameList(IEnumerable<string> names) => WriteString(string.Join(',', names));

    public SshWriter WriteMPInt(ReadOnlySpan<byte> unsignedBigEndian)
    {
        var trimmed = unsignedBigEndian.TrimStart((byte)0);
        var needsPadding = !trimmed.IsEmpty && (trimmed[0] & 0x80) != 0;
        WriteUInt32((uint)(trimmed.Length + (needsPadding ? 1 : 0)));
        if (needsPadding)
        {
            WriteByte(0);
        }

        return WriteRaw(trimmed);
    }

    public SshWriter WriteMPInt(BigInteger value)
    {
        if (value.Sign < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "SSH mpints we write are never negative.");
        }

        return WriteMPInt(value.ToByteArray(isUnsigned: true, isBigEndian: true));
    }
}
