using System.Text;
using Avalonia.Input;
using Datapoint.Terminal.Emulator.Screen;

namespace Datapoint.Terminal.Controls.Input;

// Modifier parameters follow xterm: 1 + Shift (1) + Alt (2) + Ctrl (4).
public static class KeyEncoder
{
    private const string Escape = "\e";

    public static byte[]? Encode(Key key, KeyModifiers modifiers, TerminalModes modes, bool altSendsEscape)
    {
        ArgumentNullException.ThrowIfNull(modes);
        var sequence = EncodeSpecial(key, modifiers, modes) ?? EncodeControl(key, modifiers) ?? EncodeAlt(key, modifiers, altSendsEscape);
        return sequence is null ? null : Encoding.UTF8.GetBytes(sequence);
    }

    private static int ModifierParameter(KeyModifiers modifiers)
    {
        var value = 1;
        value += modifiers.HasFlag(KeyModifiers.Shift) ? 1 : 0;
        value += modifiers.HasFlag(KeyModifiers.Alt) ? 2 : 0;
        value += modifiers.HasFlag(KeyModifiers.Control) ? 4 : 0;
        return value;
    }

    private static string? EncodeSpecial(Key key, KeyModifiers modifiers, TerminalModes modes)
    {
        var alt = modifiers.HasFlag(KeyModifiers.Alt) ? Escape : string.Empty;
        return key switch
        {
            Key.Enter => alt + (modes.LineFeedNewLine ? "\r\n" : "\r"),
            Key.Back => modifiers.HasFlag(KeyModifiers.Control) ? "\b" : alt + "\x7f",
            Key.Tab => modifiers.HasFlag(KeyModifiers.Shift) ? "\e[Z" : alt + "\t",
            Key.Escape => "\e",
            Key.Up => Cursor('A', modifiers, modes),
            Key.Down => Cursor('B', modifiers, modes),
            Key.Right => Cursor('C', modifiers, modes),
            Key.Left => Cursor('D', modifiers, modes),
            Key.Home => Cursor('H', modifiers, modes),
            Key.End => Cursor('F', modifiers, modes),
            Key.Insert => Tilde(2, modifiers),
            Key.Delete => Tilde(3, modifiers),
            Key.PageUp => Tilde(5, modifiers),
            Key.PageDown => Tilde(6, modifiers),
            >= Key.F1 and <= Key.F4 => FunctionKey('P' + (key - Key.F1), modifiers),
            >= Key.F5 and <= Key.F12 => Tilde(s_functionKeyCodes[key - Key.F5], modifiers),
            _ => null,
        };
    }

    private static readonly int[] s_functionKeyCodes = [15, 17, 18, 19, 20, 21, 23, 24];

    private static string Cursor(char final, KeyModifiers modifiers, TerminalModes modes)
    {
        var parameter = ModifierParameter(modifiers);
        if (parameter > 1)
        {
            return $"\e[1;{parameter}{final}";
        }

        return modes.ApplicationCursorKeys ? $"\eO{final}" : $"\e[{final}";
    }

    private static string FunctionKey(int final, KeyModifiers modifiers)
    {
        var parameter = ModifierParameter(modifiers);
        return parameter > 1 ? $"\e[1;{parameter}{(char)final}" : $"\eO{(char)final}";
    }

    private static string Tilde(int code, KeyModifiers modifiers)
    {
        var parameter = ModifierParameter(modifiers);
        return parameter > 1 ? $"\e[{code};{parameter}~" : $"\e[{code}~";
    }

    private static string? EncodeControl(Key key, KeyModifiers modifiers)
    {
        if (!modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta))
        {
            return null;
        }

        var alt = modifiers.HasFlag(KeyModifiers.Alt) ? Escape : string.Empty;
        char? control = key switch
        {
            >= Key.A and <= Key.Z => (char)(key - Key.A + 1),
            Key.Space or Key.D2 => '\0',
            Key.OemOpenBrackets or Key.D3 => '\e',
            Key.OemPipe or Key.OemBackslash or Key.D4 => '\x1c',
            Key.OemCloseBrackets or Key.D5 => '\x1d',
            Key.D6 => '\x1e',
            Key.OemMinus or Key.OemQuestion or Key.D7 => '\x1f',
            Key.D8 => '\x7f',
            _ => null,
        };
        return control is null ? null : alt + control;
    }

    private static string? EncodeAlt(Key key, KeyModifiers modifiers, bool altSendsEscape)
    {
        var isPlainAlt = altSendsEscape && modifiers.HasFlag(KeyModifiers.Alt) && !modifiers.HasFlag(KeyModifiers.Control) && !modifiers.HasFlag(KeyModifiers.Meta);
        if (!isPlainAlt)
        {
            return null;
        }

        var shift = modifiers.HasFlag(KeyModifiers.Shift);
        return key switch
        {
            >= Key.A and <= Key.Z => Escape + (char)((shift ? 'A' : 'a') + (key - Key.A)),
            >= Key.D0 and <= Key.D9 when !shift => Escape + (char)('0' + (key - Key.D0)),
            Key.Space => Escape + " ",
            _ => null,
        };
    }
}
