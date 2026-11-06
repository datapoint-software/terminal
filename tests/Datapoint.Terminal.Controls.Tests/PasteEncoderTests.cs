using System.Text;
using Datapoint.Terminal.Controls.Input;

namespace Datapoint.Terminal.Controls.Tests;

public sealed class PasteEncoderTests
{
    [Fact]
    public void Turns_line_breaks_into_carriage_returns() =>
        Assert.Equal("a\rb\rc", Encoding.UTF8.GetString(PasteEncoder.Encode("a\r\nb\nc", bracketed: false)));

    [Fact]
    public void Wraps_a_bracketed_paste_in_its_markers() =>
        Assert.Equal("\e[200~ls\e[201~", Encoding.UTF8.GetString(PasteEncoder.Encode("ls", bracketed: true)));

    [Fact]
    public void Removes_an_end_marker_hidden_in_pasted_text() =>
        Assert.Equal("\e[200~harmlessrm -rf ~\r\e[201~", Encoding.UTF8.GetString(PasteEncoder.Encode("harmless\e[201~rm -rf ~\n", bracketed: true)));
}
