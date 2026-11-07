using Datapoint.Terminal.Emulator.Screen;

namespace Datapoint.Terminal.Shell;

// Applies a theme to a terminal. Programs can still change colours with escape sequences until the theme is next applied.
public static class TerminalThemes
{
    public static bool IsDark(TerminalTheme theme, bool systemIsDark) => theme switch
    {
        TerminalTheme.Light => false,
        TerminalTheme.System => systemIsDark,
        _ => true,
    };

    public static void Apply(ColorPalette palette, bool isDark) => Apply(palette, isDark ? Themes.BuiltInThemes.Dark : Themes.BuiltInThemes.Light);

    public static void Apply(ColorPalette palette, Themes.ColorTheme theme)
    {
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentNullException.ThrowIfNull(theme);
        for (var index = 0; index < theme.Ansi.Count; index++)
        {
            palette[index] = theme.Ansi[index];
        }

        palette.Foreground = theme.Foreground;
        palette.Background = theme.Background;
        palette.CursorColor = theme.Cursor;
    }
}
