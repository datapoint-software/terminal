using System.Text;
using Avalonia.Input;
using Datapoint.Terminal.Controls.Input;
using Datapoint.Terminal.Emulator.Screen;

namespace Datapoint.Terminal.Controls.Tests;

public sealed class KeyEncoderTests
{
    private static string? Encode(Key key, KeyModifiers modifiers = KeyModifiers.None, bool applicationCursorKeys = false, bool altSendsEscape = true)
    {
        var bytes = KeyEncoder.Encode(key, modifiers, new TerminalModes { ApplicationCursorKeys = applicationCursorKeys }, altSendsEscape);
        return bytes is null ? null : Encoding.UTF8.GetString(bytes);
    }

    [Theory]
    [InlineData(Key.Enter, "\r")]
    [InlineData(Key.Back, "\x7f")]
    [InlineData(Key.Tab, "\t")]
    [InlineData(Key.Escape, "\e")]
    [InlineData(Key.Up, "\e[A")]
    [InlineData(Key.Home, "\e[H")]
    [InlineData(Key.Delete, "\e[3~")]
    [InlineData(Key.PageDown, "\e[6~")]
    [InlineData(Key.F1, "\eOP")]
    [InlineData(Key.F5, "\e[15~")]
    [InlineData(Key.F12, "\e[24~")]
    public void Encodes_special_keys_as_xterm_does(Key key, string expected) => Assert.Equal(expected, Encode(key));

    [Fact]
    public void Uses_application_cursor_keys_when_the_program_asks() => Assert.Equal("\eOA", Encode(Key.Up, applicationCursorKeys: true));

    [Theory]
    [InlineData(Key.Right, KeyModifiers.Control, "\e[1;5C")]
    [InlineData(Key.Left, KeyModifiers.Alt | KeyModifiers.Shift, "\e[1;4D")]
    [InlineData(Key.Delete, KeyModifiers.Control, "\e[3;5~")]
    [InlineData(Key.F1, KeyModifiers.Shift, "\e[1;2P")]
    [InlineData(Key.Tab, KeyModifiers.Shift, "\e[Z")]
    public void Adds_modifier_parameters(Key key, KeyModifiers modifiers, string expected) => Assert.Equal(expected, Encode(key, modifiers));

    [Theory]
    [InlineData(Key.C, "\x03")]
    [InlineData(Key.A, "\x01")]
    [InlineData(Key.Z, "\x1a")]
    [InlineData(Key.Space, "\0")]
    [InlineData(Key.OemOpenBrackets, "\e")]
    public void Encodes_control_keys(Key key, string expected) => Assert.Equal(expected, Encode(key, KeyModifiers.Control));

    [Fact]
    public void Sends_escape_before_a_letter_typed_with_alt() => Assert.Equal("\eb", Encode(Key.B, KeyModifiers.Alt));

    [Fact]
    public void Leaves_alt_letters_to_text_input_when_alt_types_characters() => Assert.Null(Encode(Key.B, KeyModifiers.Alt, altSendsEscape: false));

    [Fact]
    public void Leaves_plain_letters_to_text_input() => Assert.Null(Encode(Key.A));

    [Fact]
    public void Never_turns_command_shortcuts_into_control_characters() => Assert.Null(Encode(Key.C, KeyModifiers.Control | KeyModifiers.Meta));
}
