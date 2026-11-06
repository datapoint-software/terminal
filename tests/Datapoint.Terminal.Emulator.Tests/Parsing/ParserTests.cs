using System.Text;
using Datapoint.Terminal.Emulator.Parsing;

namespace Datapoint.Terminal.Emulator.Tests.Parsing;

public sealed class ParserTests
{
    private static IReadOnlyList<string> Parse(params string[] chunks)
    {
        var handler = new RecordingHandler();
        var parser = new Parser(handler, maxStringLength: 16);
        foreach (var chunk in chunks)
        {
            parser.Advance(Encoding.UTF8.GetBytes(chunk));
        }

        return handler.Finish();
    }

    private static IReadOnlyList<string> ParseBytes(params byte[] bytes)
    {
        var handler = new RecordingHandler();
        new Parser(handler).Advance(bytes);
        return handler.Finish();
    }

    [Fact]
    public void Prints_plain_text()
    {
        Assert.Equal(["print hello"], Parse("hello"));
    }

    [Fact]
    public void Executes_control_characters_between_text()
    {
        Assert.Equal(["print a", "execute 0D", "execute 0A", "print b"], Parse("a\r\nb"));
    }

    [Fact]
    public void Decodes_utf8_split_across_reads()
    {
        var bytes = Encoding.UTF8.GetBytes("é€😀");
        var handler = new RecordingHandler();
        var parser = new Parser(handler);

        parser.Advance(bytes.AsSpan(0, 3));
        parser.Advance(bytes.AsSpan(3, 3));
        parser.Advance(bytes.AsSpan(6));

        Assert.Equal(["print é€😀"], handler.Finish());
    }

    [Fact]
    public void Replaces_an_invalid_byte_with_the_replacement_character()
    {
        Assert.Equal(["print a�b"], ParseBytes(0x61, 0xFF, 0x62));
    }

    [Fact]
    public void Replaces_an_interrupted_sequence_and_keeps_the_byte_that_interrupted_it()
    {
        Assert.Equal(["print �a"], ParseBytes(0xC3, 0x61));
    }

    [Fact]
    public void Replaces_an_overlong_encoding()
    {
        Assert.Equal(["print �"], ParseBytes(0xE0, 0x80, 0xAF));
    }

    [Fact]
    public void Replaces_an_encoded_surrogate()
    {
        Assert.Equal(["print �"], ParseBytes(0xED, 0xA0, 0x80));
    }

    [Fact]
    public void Does_not_treat_c1_bytes_as_controls()
    {
        Assert.Equal(["print �[31m"], ParseBytes(0x9B, 0x5B, 0x33, 0x31, 0x6D));
    }

    [Fact]
    public void Dispatches_a_csi_sequence_with_parameters()
    {
        Assert.Equal(["csi 1;31m"], Parse("\e[1;31m"));
    }

    [Fact]
    public void Keeps_an_empty_parameter_as_zero()
    {
        Assert.Equal(["csi 0;5H"], Parse("\e[;5H"));
    }

    [Fact]
    public void Keeps_sub_parameters_apart_from_parameters()
    {
        Assert.Equal(["csi 38:2:0:255:0:0;1m"], Parse("\e[38:2:0:255:0:0;1m"));
    }

    [Fact]
    public void Dispatches_a_private_marker()
    {
        Assert.Equal(["csi ?1049h"], Parse("\e[?1049h"));
    }

    [Fact]
    public void Dispatches_intermediates()
    {
        Assert.Equal(["csi 2 q"], Parse("\e[2 q"));
    }

    [Fact]
    public void Clamps_large_parameter_values()
    {
        Assert.Equal(["csi 65535A"], Parse("\e[99999999A"));
    }

    [Fact]
    public void Ignores_a_sequence_with_too_many_parameters()
    {
        var parameters = string.Join(';', Enumerable.Repeat("1", Parser.MaxParameters + 1));

        Assert.Equal(["print x"], Parse($"\e[{parameters}mx"));
    }

    [Fact]
    public void Ignores_a_sequence_with_a_private_marker_after_parameters()
    {
        Assert.Equal(["print x"], Parse("\e[1?hx"));
    }

    [Fact]
    public void Executes_controls_inside_a_csi_sequence()
    {
        Assert.Equal(["execute 0A", "csi 2J"], Parse("\e[2\nJ"));
    }

    [Fact]
    public void Cancels_a_sequence_on_can()
    {
        Assert.Equal(["execute 18", "print x"], Parse("\e[31\u0018x"));
    }

    [Fact]
    public void Restarts_on_escape_inside_a_sequence()
    {
        Assert.Equal(["csi 2J"], Parse("\e[31\e[2J"));
    }

    [Fact]
    public void Dispatches_an_escape_sequence()
    {
        Assert.Equal(["esc 7", "esc (B"], Parse("\e7\e(B"));
    }

    [Fact]
    public void Dispatches_an_osc_string_ended_by_bell()
    {
        Assert.Equal(["osc 0;title"], Parse("\e]0;title\a"));
    }

    [Fact]
    public void Dispatches_an_osc_string_ended_by_string_terminator()
    {
        Assert.Equal(["osc 0;title", @"esc \"], Parse("\e]0;title\e\\"));
    }

    [Fact]
    public void Keeps_utf8_inside_an_osc_string()
    {
        Assert.Equal(["osc 2;café"], Parse("\e]2;café\a"));
    }

    [Fact]
    public void Drops_an_osc_string_cancelled_by_can()
    {
        Assert.Equal(["print x"], Parse("\e]0;title\u0018x"));
    }

    [Fact]
    public void Drops_an_osc_string_longer_than_the_limit()
    {
        Assert.Equal(["print x"], Parse("\e]0;this title is far too long\ax"));
    }

    [Fact]
    public void Passes_dcs_data_through_between_hook_and_unhook()
    {
        Assert.Equal(["dcs hook 1$q", "dcs put m", "dcs unhook", @"esc \"], Parse("\eP1$qm\e\\"));
    }

    [Fact]
    public void Dispatches_an_apc_string()
    {
        Assert.Equal(["apc Gf=1", @"esc \"], Parse("\e_Gf=1\e\\"));
    }

    [Fact]
    public void Ignores_sos_and_pm_strings()
    {
        Assert.Equal([@"esc \", "print x"], Parse("\eXhidden\e\\x"));
    }

    [Fact]
    public void Ignores_delete_in_ground()
    {
        Assert.Equal(["print ab"], Parse("a\u007Fb"));
    }
}
