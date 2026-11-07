using Datapoint.Terminal.Themes;

namespace Datapoint.Terminal.Tests.Themes;

public sealed class ThemeImportTests
{
    private const string ITerm = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><plist version=\"1.0\"><dict><key>Ansi 0 Color</key><dict><key>Blue Component</key><real>0.0</real><key>Green Component</key><real>0.0</real><key>Red Component</key><real>0.0</real></dict><key>Ansi 1 Color</key><dict><key>Blue Component</key><real>0.19215686274509805</real><key>Green Component</key><real>0.19215686274509805</real><key>Red Component</key><real>0.803921568627451</real></dict><key>Ansi 2 Color</key><dict><key>Blue Component</key><real>0.4745098039215686</real><key>Green Component</key><real>0.7372549019607844</real><key>Red Component</key><real>0.050980392156862744</real></dict><key>Ansi 3 Color</key><dict><key>Blue Component</key><real>0.06274509803921569</real><key>Green Component</key><real>0.8980392156862745</real><key>Red Component</key><real>0.8980392156862745</real></dict><key>Ansi 4 Color</key><dict><key>Blue Component</key><real>0.7843137254901961</real><key>Green Component</key><real>0.4470588235294118</real><key>Red Component</key><real>0.1411764705882353</real></dict><key>Ansi 5 Color</key><dict><key>Blue Component</key><real>0.7372549019607844</real><key>Green Component</key><real>0.24705882352941178</real><key>Red Component</key><real>0.7372549019607844</real></dict><key>Ansi 6 Color</key><dict><key>Blue Component</key><real>0.803921568627451</real><key>Green Component</key><real>0.6588235294117647</real><key>Red Component</key><real>0.06666666666666667</real></dict><key>Ansi 7 Color</key><dict><key>Blue Component</key><real>0.8980392156862745</real><key>Green Component</key><real>0.8980392156862745</real><key>Red Component</key><real>0.8980392156862745</real></dict><key>Ansi 8 Color</key><dict><key>Blue Component</key><real>0.4</real><key>Green Component</key><real>0.4</real><key>Red Component</key><real>0.4</real></dict><key>Ansi 9 Color</key><dict><key>Blue Component</key><real>0.2980392156862745</real><key>Green Component</key><real>0.2980392156862745</real><key>Red Component</key><real>0.9450980392156862</real></dict><key>Ansi 10 Color</key><dict><key>Blue Component</key><real>0.5450980392156862</real><key>Green Component</key><real>0.8196078431372549</real><key>Red Component</key><real>0.13725490196078433</real></dict><key>Ansi 11 Color</key><dict><key>Blue Component</key><real>0.2627450980392157</real><key>Green Component</key><real>0.9607843137254902</real><key>Red Component</key><real>0.9607843137254902</real></dict><key>Ansi 12 Color</key><dict><key>Blue Component</key><real>0.9176470588235294</real><key>Green Component</key><real>0.5568627450980392</real><key>Red Component</key><real>0.23137254901960785</real></dict><key>Ansi 13 Color</key><dict><key>Blue Component</key><real>0.8392156862745098</real><key>Green Component</key><real>0.4392156862745098</real><key>Red Component</key><real>0.8392156862745098</real></dict><key>Ansi 14 Color</key><dict><key>Blue Component</key><real>0.8588235294117647</real><key>Green Component</key><real>0.7215686274509804</real><key>Red Component</key><real>0.1607843137254902</real></dict><key>Ansi 15 Color</key><dict><key>Blue Component</key><real>1.0</real><key>Green Component</key><real>1.0</real><key>Red Component</key><real>1.0</real></dict><key>Background Color</key><dict><key>Blue Component</key><real>0.11764705882352941</real><key>Green Component</key><real>0.11764705882352941</real><key>Red Component</key><real>0.11764705882352941</real></dict><key>Foreground Color</key><dict><key>Blue Component</key><real>0.8</real><key>Green Component</key><real>0.8</real><key>Red Component</key><real>0.8</real></dict><key>Cursor Color</key><dict><key>Blue Component</key><real>0.0</real><key>Green Component</key><real>0.8</real><key>Red Component</key><real>1.0</real></dict></dict></plist>";

    private const string WindowsTerminal = "{ \"name\": \"Campbell Copy\", \"background\": \"#0C0C0C\", \"foreground\": \"#CCCCCC\", \"cursorColor\": \"#FFFFFF\", \"selectionBackground\": \"#334455\", \"black\": \"#000000\", \"red\": \"#cd3131\", \"green\": \"#0dbc79\", \"yellow\": \"#e5e510\", \"blue\": \"#2472c8\", \"purple\": \"#bc3fbc\", \"cyan\": \"#11a8cd\", \"white\": \"#e5e5e5\", \"brightBlack\": \"#666666\", \"brightRed\": \"#f14c4c\", \"brightGreen\": \"#23d18b\", \"brightYellow\": \"#f5f543\", \"brightBlue\": \"#3b8eea\", \"brightPurple\": \"#d670d6\", \"brightCyan\": \"#29b8db\", \"brightWhite\": \"#ffffff\" }";

    private const string Ghostty = "palette = 0=#000000\npalette = 1=#cd3131\npalette = 2=#0dbc79\npalette = 3=#e5e510\npalette = 4=#2472c8\npalette = 5=#bc3fbc\npalette = 6=#11a8cd\npalette = 7=#e5e5e5\npalette = 8=#666666\npalette = 9=#f14c4c\npalette = 10=#23d18b\npalette = 11=#f5f543\npalette = 12=#3b8eea\npalette = 13=#d670d6\npalette = 14=#29b8db\npalette = 15=#ffffff\nbackground = #101010\nforeground = #f0f0f0\ncursor-color = #00ff00";

    [Fact]
    public void Reads_an_iterm2_colour_file()
    {
        var (theme, problem) = ThemeImport.Read("Solar.itermcolors", ITerm);

        Assert.Null(problem);
        Assert.Equal("Solar", theme!.Name);
        Assert.Equal("iTerm2", theme.Source);
        Assert.Equal(0xCD3131, theme.Ansi[1]);
        Assert.Equal(0x1E1E1E, theme.Background);
        Assert.Equal(0xFFCC00, theme.Cursor);
        Assert.Null(theme.Selection);
        Assert.True(theme.IsDark);
    }

    [Fact]
    public void Reads_a_windows_terminal_scheme_with_its_own_name()
    {
        var (theme, _) = ThemeImport.Read("scheme.json", WindowsTerminal);

        Assert.Equal("Campbell Copy", theme!.Name);
        Assert.Equal(0x0DBC79, theme.Ansi[2]);
        Assert.Equal(0x334455, theme.Selection);
    }

    [Fact]
    public void Reads_a_ghostty_theme()
    {
        var (theme, _) = ThemeImport.Read("night", Ghostty);

        Assert.Equal("Ghostty", theme!.Source);
        Assert.Equal(0x00FF00, theme.Cursor);
        Assert.Equal(0xFFFFFF, theme.Ansi[15]);
    }

    [Fact]
    public void Explains_what_a_theme_lacks()
    {
        var (theme, problem) = ThemeImport.Read("half.conf", "background #000000\nforeground #ffffff\ncolor0 #000000");

        Assert.Null(theme);
        Assert.Equal("colour 1 is missing", problem);
        Assert.NotNull(ThemeImport.Read("bad.json", "{ not json").Problem);
    }
}
