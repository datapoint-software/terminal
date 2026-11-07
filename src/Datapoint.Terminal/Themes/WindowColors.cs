using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;

namespace Datapoint.Terminal.Themes;

// The window's theme, chosen apart from the terminal's. Taking the terminal theme's colours, the window's surfaces and text are
// its background and text colours mixed in steps, so panes, cards and separators keep their relative depth. Accent and status
// colours stay ours, for contrast.
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

    // The terminal theme matters only when the window takes its colours; one that follows the system keeps the window following
    // the system's light or dark appearance too.
    public static void Apply(WindowTheme window, ColorTheme terminal, bool terminalFollowsSystem)
    {
        ArgumentNullException.ThrowIfNull(terminal);
        if (Application.Current is not { } application)
        {
            return;
        }

        var matched = window == WindowTheme.Terminal ? terminal : null;
        foreach (var (key, color) in s_brushes)
        {
            if (matched is null)
            {
                application.Resources.Remove(key);
                continue;
            }

            application.Resources[key] = new SolidColorBrush(color(matched));
        }

        application.RequestedThemeVariant = Variant(window, terminal, terminalFollowsSystem);
    }

    public static ThemeVariant Variant(WindowTheme window, ColorTheme terminal, bool terminalFollowsSystem)
    {
        ArgumentNullException.ThrowIfNull(terminal);
        return window switch
        {
            WindowTheme.Light => ThemeVariant.Light,
            WindowTheme.Dark => ThemeVariant.Dark,
            WindowTheme.Terminal when !terminalFollowsSystem => terminal.IsDark ? ThemeVariant.Dark : ThemeVariant.Light,
            _ => ThemeVariant.Default,
        };
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
