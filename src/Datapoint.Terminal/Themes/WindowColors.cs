using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;

namespace Datapoint.Terminal.Themes;

// "Match the window to the terminal theme": the window's surfaces and text take the theme's background and text colours, mixed
// in steps, so panes, cards and separators keep their relative depth. Accent and status colours stay ours, for contrast.
public static class WindowColors
{
    private static readonly (string Key, Func<ColorTheme, Color> Color)[] s_brushes =
    [
        ("WindowBrush", theme => Mix(theme, 0)),
        ("PaneBrush", theme => Mix(theme, 0.04)),
        ("GroupedBackgroundBrush", theme => Mix(theme, 0.03)),
        ("CardBrush", theme => theme.IsDark ? Mix(theme, 0.08) : Mix(theme, 0)),
        ("FieldBrush", theme => theme.IsDark ? Mix(theme, 0.08) : Mix(theme, 0)),
        ("SegmentBrush", theme => theme.IsDark ? Mix(theme, 0.3) : Mix(theme, 0)),
        ("FillBrush", theme => Mix(theme, 0.1)),
        ("FillHoverBrush", theme => Mix(theme, 0.16)),
        ("CodeBrush", theme => Mix(theme, 0.06)),
        ("SeparatorBrush", theme => Mix(theme, 0.15)),
        ("LabelBrush", theme => Mix(theme, 1)),
        ("SecondaryLabelBrush", theme => Mix(theme, 0.62)),
        ("DisabledLabelBrush", theme => Mix(theme, 0.4)),
    ];

    public static bool IsSystemDark =>
        Application.Current?.PlatformSettings?.GetColorValues().ThemeVariant == PlatformThemeVariant.Dark;

    // Null puts the window's own colours back. The system theme keeps following the system's light or dark appearance.
    public static void Apply(ColorTheme? theme, bool followsSystem)
    {
        if (Application.Current is not { } application)
        {
            return;
        }

        foreach (var (key, color) in s_brushes)
        {
            if (theme is null)
            {
                application.Resources.Remove(key);
                continue;
            }

            application.Resources[key] = new SolidColorBrush(color(theme));
        }

        application.RequestedThemeVariant = theme is null || followsSystem ? ThemeVariant.Default
            : theme.IsDark ? ThemeVariant.Dark
            : ThemeVariant.Light;
    }

    // From the background (0) towards the text colour (1).
    private static Color Mix(ColorTheme theme, double amount)
    {
        static byte Channel(int from, int to, int shift, double amount) =>
            (byte)Math.Round((((from >> shift) & 0xFF) * (1 - amount)) + (((to >> shift) & 0xFF) * amount));

        return Color.FromRgb(
            Channel(theme.Background, theme.Foreground, 16, amount),
            Channel(theme.Background, theme.Foreground, 8, amount),
            Channel(theme.Background, theme.Foreground, 0, amount));
    }
}
