using Avalonia.Styling;
using Datapoint.Terminal.Themes;

namespace Datapoint.Terminal.Tests.Themes;

public sealed class WindowThemeTests
{
    [Theory]
    [InlineData(WindowTheme.System, false, "Default")]
    [InlineData(WindowTheme.Light, false, "Light")]
    [InlineData(WindowTheme.Dark, false, "Dark")]
    [InlineData(WindowTheme.Terminal, false, "Dark")]
    [InlineData(WindowTheme.Terminal, true, "Default")]
    public void Picks_the_window_variant_apart_from_a_dark_terminal(WindowTheme window, bool terminalFollowsSystem, string expected) =>
        Assert.Equal(expected, WindowColors.Variant(window, BuiltInThemes.Dark, terminalFollowsSystem).Key.ToString());
}
