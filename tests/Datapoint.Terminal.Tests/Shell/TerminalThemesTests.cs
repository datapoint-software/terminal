using Datapoint.Terminal.Emulator.Screen;
using Datapoint.Terminal.Shell;

namespace Datapoint.Terminal.Tests.Shell;

public sealed class TerminalThemesTests
{
    [Theory]
    [InlineData(TerminalTheme.Dark, false, true)]
    [InlineData(TerminalTheme.Light, true, false)]
    [InlineData(TerminalTheme.System, true, true)]
    [InlineData(TerminalTheme.System, false, false)]
    public void Picks_light_or_dark(TerminalTheme theme, bool systemIsDark, bool expected)
    {
        Assert.Equal(expected, TerminalThemes.IsDark(theme, systemIsDark));
    }

    [Fact]
    public void Keeps_every_colour_readable_on_the_light_background()
    {
        var palette = new ColorPalette();

        TerminalThemes.Apply(palette, isDark: false);

        Assert.Equal(0xFFFFFF, palette.Background);
        for (var index = 0; index < 16; index++)
        {
            Assert.True(Contrast(palette[index], palette.Background) >= (index is 7 or 15 ? 1.5 : 3), $"colour {index}");
        }
    }

    [Fact]
    public void Restores_the_dark_palette()
    {
        var palette = new ColorPalette();
        TerminalThemes.Apply(palette, isDark: false);

        TerminalThemes.Apply(palette, isDark: true);

        Assert.Equal((0xCCCCCC, 0x1E1E1E, 0xCD3131), (palette.Foreground, palette.Background, palette[1]));
    }

    private static double Contrast(int first, int second)
    {
        static double Luminance(int rgb)
        {
            static double Channel(int value)
            {
                var c = value / 255.0;
                return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            }

            return (0.2126 * Channel((rgb >> 16) & 0xFF)) + (0.7152 * Channel((rgb >> 8) & 0xFF)) + (0.0722 * Channel(rgb & 0xFF));
        }

        var (light, dark) = (Math.Max(Luminance(first), Luminance(second)), Math.Min(Luminance(first), Luminance(second)));
        return (light + 0.05) / (dark + 0.05);
    }
}
