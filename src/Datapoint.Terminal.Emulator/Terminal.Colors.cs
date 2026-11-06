using System.Globalization;
using System.Text;
using Datapoint.Terminal.Emulator.Screen;

namespace Datapoint.Terminal.Emulator;

public sealed partial class Terminal
{
    public ColorPalette Palette { get; } = new();

    private void HandleColorQuery(ReadOnlySpan<byte> command, ReadOnlySpan<byte> argument)
    {
        var name = Encoding.ASCII.GetString(command);
        var text = Encoding.ASCII.GetString(argument);
        switch (name)
        {
            case "10" when text is "?":
                ReplyColor("10", Palette.Foreground);
                break;
            case "11" when text is "?":
                ReplyColor("11", Palette.Background);
                break;
            case "12" when text is "?":
                ReplyColor("12", Palette.CursorColor);
                break;
            case "4":
                ReplyIndexedColors(text);
                break;
        }
    }

    private void ReplyIndexedColors(string text)
    {
        var parts = text.Split(';');
        for (var index = 0; index + 1 < parts.Length; index += 2)
        {
            var isQuery = parts[index + 1] is "?";
            if (!isQuery || !int.TryParse(parts[index], CultureInfo.InvariantCulture, out var color) || color is < 0 or > 255)
            {
                continue;
            }

            ReplyColor($"4;{color}", Palette[color]);
        }
    }

    private void ReplyColor(string prefix, int rgb)
    {
        var red = (rgb >> 16) & 0xFF;
        var green = (rgb >> 8) & 0xFF;
        var blue = rgb & 0xFF;
        Reply(string.Create(CultureInfo.InvariantCulture, $"\e]{prefix};rgb:{red:x2}{red:x2}/{green:x2}{green:x2}/{blue:x2}{blue:x2}\e\\"));
    }
}
