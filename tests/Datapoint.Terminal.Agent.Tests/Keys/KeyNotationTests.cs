using System.Text;
using Datapoint.Terminal.Agent.Keys;

namespace Datapoint.Terminal.Agent.Tests.Keys;

public sealed class KeyNotationTests
{
    private static string Encode(string keys, bool applicationCursor = false) =>
        Encoding.UTF8.GetString(KeyNotation.Encode(KeyNotation.Parse(keys, out _)!, applicationCursor));

    [Theory]
    [InlineData("<Esc>:wq<Enter>", "\e:wq\r")]
    [InlineData("q", "q")]
    [InlineData("<C-c>", "\x03")]
    [InlineData("<c-X>", "\x18")]
    [InlineData("<M-f>", "\ef")]
    [InlineData("<lt>tag>", "<tag>")]
    [InlineData("a < b", "a < b")]
    [InlineData("<Down><PageDown><F5>", "\e[B\e[6~\e[15~")]
    public void Encodes_keys_as_the_keyboard_sends_them(string keys, string expected)
    {
        Assert.Equal(expected, Encode(keys));
    }

    [Fact]
    public void Follows_the_programs_cursor_key_mode()
    {
        Assert.Equal("\eOA", Encode("<Up>", applicationCursor: true));
    }

    [Theory]
    [InlineData("<Bogus>")]
    [InlineData("")]
    public void Rejects_what_it_cannot_send(string keys)
    {
        Assert.Null(KeyNotation.Parse(keys, out var problem));
        Assert.NotNull(problem);
    }

    [Fact]
    public void Shows_typed_text_as_lines_for_the_command_checks()
    {
        Assert.Equal(" :!rm -rf /\n", KeyNotation.Typed(KeyNotation.Parse("<Esc>:!rm -rf /<Enter>", out _)!));
    }
}
