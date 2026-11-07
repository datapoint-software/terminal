using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Datapoint.Terminal.Themes;

namespace Datapoint.Terminal.Settings;

// One theme in Settings › Appearance, with the colours its small preview uses. BuiltIn is set for the three built-in choices.
public sealed partial class ThemeCard(ColorTheme theme, string name, string note, Shell.TerminalTheme? builtIn) : ObservableObject
{
    public ColorTheme Theme { get; } = theme;

    public string Name { get; } = name;

    public string Note { get; } = note;

    public bool HasNote => Note.Length > 0;

    public Shell.TerminalTheme? BuiltIn { get; } = builtIn;

    public bool IsCustom => BuiltIn is null;

    public string AccessibleName => Resources.Strings.Format(Resources.Strings.ThemeCardName, HasNote ? $"{Name}, {Note}" : Name);

    public IBrush Background => Brush(Theme.Background);

    public IBrush Foreground => Brush(Theme.Foreground);

    public IBrush Prompt => Brush(Theme.Ansi[2]);

    public IBrush Folder => Brush(Theme.Ansi[4]);

    public IBrush File => Brush(Theme.Ansi[1]);

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    private static SolidColorBrush Brush(int color) => new(Color.FromRgb((byte)(color >> 16), (byte)(color >> 8), (byte)color));
}
