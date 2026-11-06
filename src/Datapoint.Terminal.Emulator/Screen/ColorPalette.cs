namespace Datapoint.Terminal.Emulator.Screen;

public sealed class ColorPalette
{
    private static readonly int[] s_ansi =
    [
        0x000000, 0xCD3131, 0x0DBC79, 0xE5E510, 0x2472C8, 0xBC3FBC, 0x11A8CD, 0xE5E5E5,
        0x666666, 0xF14C4C, 0x23D18B, 0xF5F543, 0x3B8EEA, 0xD670D6, 0x29B8DB, 0xFFFFFF,
    ];

    private readonly int[] _colors = CreateDefaultColors();

    public int Foreground { get; set; } = 0xCCCCCC;

    public int Background { get; set; } = 0x1E1E1E;

    public int CursorColor { get; set; } = 0xCCCCCC;

    public int this[int index]
    {
        get => _colors[index];
        set => _colors[index] = value;
    }

    public int Resolve(TerminalColor color, bool isForeground) => color.Kind switch
    {
        ColorKind.Indexed => _colors[color.Value],
        ColorKind.Rgb => color.Value,
        _ => isForeground ? Foreground : Background,
    };

    private static int[] CreateDefaultColors()
    {
        var colors = new int[256];
        s_ansi.CopyTo(colors, 0);
        for (var index = 16; index < 232; index++)
        {
            colors[index] = CubeColor(index - 16);
        }

        for (var index = 232; index < 256; index++)
        {
            var level = 8 + ((index - 232) * 10);
            colors[index] = (level << 16) | (level << 8) | level;
        }

        return colors;
    }

    private static int CubeColor(int index)
    {
        static int Level(int step) => step == 0 ? 0 : 55 + (step * 40);
        return (Level(index / 36) << 16) | (Level(index / 6 % 6) << 8) | Level(index % 6);
    }
}
