using Datapoint.Terminal.Emulator.Screen;

namespace Datapoint.Terminal.Shell;

// Our own palettes. Dark is the emulator's built-in one; Light keeps every colour readable on white, so "white" text is a light
// grey and yellow is a dark gold. Programs can still change colours with escape sequences until the theme is next applied.
public static class TerminalThemes
{
    private static readonly int[] s_dark =
    [
        0x000000, 0xCD3131, 0x0DBC79, 0xE5E510, 0x2472C8, 0xBC3FBC, 0x11A8CD, 0xE5E5E5,
        0x666666, 0xF14C4C, 0x23D18B, 0xF5F543, 0x3B8EEA, 0xD670D6, 0x29B8DB, 0xFFFFFF,
    ];

    private static readonly int[] s_light =
    [
        0x1F1F1F, 0xB42318, 0x1E7B34, 0x8A6A00, 0x1F5FBF, 0x8E3BB0, 0x0E7490, 0xA8A8A8,
        0x5C5C5C, 0xD92D20, 0x2E9E4B, 0xA67C00, 0x2F80ED, 0xA855F7, 0x0891B2, 0xC8C8C8,
    ];

    public static bool IsDark(TerminalTheme theme, bool systemIsDark) => theme switch
    {
        TerminalTheme.Light => false,
        TerminalTheme.System => systemIsDark,
        _ => true,
    };

    public static void Apply(ColorPalette palette, bool isDark)
    {
        ArgumentNullException.ThrowIfNull(palette);
        var ansi = isDark ? s_dark : s_light;
        for (var index = 0; index < ansi.Length; index++)
        {
            palette[index] = ansi[index];
        }

        palette.Foreground = isDark ? 0xCCCCCC : 0x1F1F1F;
        palette.Background = isDark ? 0x1E1E1E : 0xFFFFFF;
        palette.CursorColor = palette.Foreground;
    }
}
