using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Shell;
using Datapoint.Terminal.Themes;

namespace Datapoint.Terminal.Settings;

// Settings › Appearance (product decisions 44 and 110): the window's theme, and the terminal's theme, font and size.
public sealed partial class AppearanceViewModel : ObservableObject
{
    private readonly SettingsViewModel _settings;
    private bool _isLoading;

    public AppearanceViewModel(SettingsViewModel settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
        Fonts = [new TextChoice(() => Strings.AppearanceFontDefault), .. MonospaceFonts.Installed().Select(font => new TextChoice(() => font))];
        settings.AppearanceChanged += (_, _) => Load();
        Load();
    }

    public ObservableCollection<ThemeCard> Themes { get; } = [];

    // The first is the default font, named in the interface's language.
    public IReadOnlyList<TextChoice> Fonts { get; }

    public IReadOnlyList<TextChoice> Sizes { get; } =
        [.. Enumerable.Range(9, 16).Select(size => new TextChoice(() => Strings.Format(Strings.AppearanceTextSizeValue, size)))];

    // In the order of WindowTheme's values.
    public IReadOnlyList<TextChoice> WindowThemes { get; } =
        [new(() => Strings.WindowThemeSystem), new(() => Strings.WindowThemeLight), new(() => Strings.WindowThemeDark), new(() => Strings.WindowThemeTerminal)];

    // In the order of AppLanguage's values. Each language is named in itself, so it can be found whatever is shown now.
    public IReadOnlyList<TextChoice> Languages { get; } =
    [
        new(() => Strings.Format(Strings.AppearanceLanguageSystem, LanguageName(Localizer.Resolve(AppLanguage.System, Localizer.Instance.SystemCulture)))),
        new(() => Strings.LanguageEnglish),
        new(() => Strings.LanguagePortuguese),
    ];

    [ObservableProperty]
    public partial int SelectedLanguage { get; set; }

    [ObservableProperty]
    public partial ThemeCard? SelectedTheme { get; set; }

    [ObservableProperty]
    public partial int SelectedFont { get; set; }

    [ObservableProperty]
    public partial int SelectedSize { get; set; }

    [ObservableProperty]
    public partial int SelectedWindowTheme { get; set; }

    [ObservableProperty]
    public partial bool ReduceMotion { get; set; }

    // The theme Edit Colors starts from: the chosen one.
    public ColorTheme CurrentTheme => _settings.ActiveTheme(WindowColors.IsSystemDark);

    public void Save(ColorTheme theme) => _settings.SaveTheme(theme);

    public void Remove(ThemeCard card)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (!card.IsCustom)
        {
            return;
        }

        _settings.RemoveTheme(card.Theme.Id);
    }

    partial void OnSelectedThemeChanged(ThemeCard? value)
    {
        if (_isLoading || value is null)
        {
            return;
        }

        if (value.BuiltIn is { } builtIn)
        {
            _settings.UseTheme(builtIn);
            return;
        }

        _settings.UseCustomTheme(value.Theme.Id);
    }

    partial void OnSelectedFontChanged(int value)
    {
        if (_isLoading || value < 0 || value >= Fonts.Count)
        {
            return;
        }

        var font = value == 0 ? null : Fonts[value].Text;
        _settings.SetAppearance(_settings.TerminalTheme, font, _settings.TerminalFontSize);
    }

    partial void OnSelectedSizeChanged(int value)
    {
        if (_isLoading || value < 0)
        {
            return;
        }

        _settings.SetAppearance(_settings.TerminalTheme, _settings.TerminalFont, value + 9);
    }

    partial void OnSelectedWindowThemeChanged(int value)
    {
        if (_isLoading || !Enum.IsDefined((WindowTheme)value))
        {
            return;
        }

        _settings.SetWindowTheme((WindowTheme)value);
    }

    partial void OnSelectedLanguageChanged(int value)
    {
        if (_isLoading || !Enum.IsDefined((AppLanguage)value) || (AppLanguage)value == _settings.Language)
        {
            return;
        }

        _settings.SetLanguage((AppLanguage)value);
    }

    private int IndexOfFont(string font)
    {
        for (var index = 1; index < Fonts.Count; index++)
        {
            if (string.Equals(Fonts[index].Text, font, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static string LanguageName(System.Globalization.CultureInfo culture) =>
        string.Equals(culture.TwoLetterISOLanguageName, "pt", StringComparison.Ordinal) ? Strings.LanguagePortuguese : Strings.LanguageEnglish;

    partial void OnReduceMotionChanged(bool value)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.SetReduceMotion(value);
    }

    private void Load()
    {
        _isLoading = true;
        var isDark = WindowColors.IsSystemDark;
        Themes.Clear();
        Themes.Add(new ThemeCard(isDark ? BuiltInThemes.Dark : BuiltInThemes.Light, Strings.ThemeDefault, Strings.ThemeFollowsSystem, TerminalTheme.System));
        Themes.Add(new ThemeCard(BuiltInThemes.Dark, BuiltInThemes.Dark.Name, string.Empty, TerminalTheme.Dark));
        Themes.Add(new ThemeCard(BuiltInThemes.Light, BuiltInThemes.Light.Name, string.Empty, TerminalTheme.Light));
        foreach (var theme in _settings.Themes)
        {
            Themes.Add(new ThemeCard(theme, theme.Name, theme.Source is null ? Strings.ThemeCustom : Strings.ThemeImported, builtIn: null));
        }

        var chosen = Themes.FirstOrDefault(card => _settings.CustomThemeId is { } id
            ? string.Equals(card.Theme.Id, id, StringComparison.Ordinal) && card.IsCustom
            : card.BuiltIn == _settings.TerminalTheme);
        foreach (var card in Themes)
        {
            card.IsSelected = card == chosen;
        }

        SelectedTheme = chosen;
        SelectedFont = _settings.TerminalFont is { } font ? Math.Max(0, IndexOfFont(font)) : 0;
        SelectedSize = Math.Clamp((int)Math.Round(_settings.TerminalFontSize, MidpointRounding.AwayFromZero) - 9, 0, Sizes.Count - 1);
        SelectedWindowTheme = (int)_settings.WindowTheme;
        ReduceMotion = _settings.ReduceMotion;
        SelectedLanguage = (int)_settings.Language;
        _isLoading = false;
    }

    internal static string Hex(int color) => "#" + color.ToString("X6", CultureInfo.InvariantCulture);
}
