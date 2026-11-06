using System.Numerics;
using Datapoint.Terminal.Sessions.Ssh;
using Datapoint.Terminal.Sessions.Ssh.Protocol;

namespace Datapoint.Terminal.Sessions.Tests.Ssh;

public sealed class WireFormatTests
{
    [Theory]
    [InlineData("0", "00000000")]
    [InlineData("9a378f9b2e332a7", "0000000809a378f9b2e332a7")]
    [InlineData("80", "000000020080")]
    public void Writes_mpints_as_in_rfc_4251(string hex, string expected)
    {
        var value = BigInteger.Parse("0" + hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);

        var written = new SshWriter().WriteMPInt(value).ToArray();

        Assert.Equal(expected, Convert.ToHexStringLower(written));
    }

    [Fact]
    public void Reads_back_what_it_writes()
    {
        var payload = new SshWriter().WriteByte(7).WriteBoolean(true).WriteUInt32(42).WriteString("héllo").WriteNameList(["a", "b"]).ToArray();

        var reader = new SshReader(payload);

        Assert.Equal(7, reader.ReadByte());
        Assert.True(reader.ReadBoolean());
        Assert.Equal(42u, reader.ReadUInt32());
        Assert.Equal("héllo", reader.ReadUtf8String());
        Assert.Equal(["a", "b"], reader.ReadNameList());
        Assert.True(reader.IsAtEnd);
    }

    [Fact]
    public void Refuses_a_string_longer_than_the_data()
    {
        byte[] payload = [0, 0, 0, 9, 1, 2];

        Assert.Throws<SshProtocolException>(() => new SshReader(payload).ReadString().Length);
    }

    [Fact]
    public void Strips_control_characters_from_server_text()
    {
        var text = ServerText.Sanitize("hi\e]0;owned\a there‮\nnext");

        Assert.Equal("hi]0;owned there\nnext", text);
    }
}
