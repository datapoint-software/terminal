using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Shell;

// Product decision 44: themes and fonts, for every window at once.
public sealed partial class MainWindow
{
    private readonly Dictionary<TerminalTheme, NativeMenuItem> _themeItems = [];
    private NativeMenu? _fontMenu;

    private void ApplyAppearance()
    {
        foreach (var tab in _tabs)
        {
            ApplyAppearance(tab);
        }

        foreach (var (theme, item) in _themeItems)
        {
            item.IsChecked = theme == _settings.TerminalTheme;
        }
    }

    private void ApplyAppearance(TerminalTab tab) => ApplyAppearance(tab.View, tab.Connection?.Terminal);

    // Also for the recordings player, so a recording looks like the terminal it came from.
    private void ApplyAppearance(Controls.TerminalView view, Emulator.Terminal? terminal)
    {
        var isDark = TerminalThemes.IsDark(_settings.TerminalTheme, ActualThemeVariant == ThemeVariant.Dark);
        if (terminal is not null)
        {
            lock (terminal.SyncRoot)
            {
                TerminalThemes.Apply(terminal.Palette, isDark);
            }
        }

        view.FontSize = _settings.TerminalFontSize;
        if (_settings.TerminalFont is { } font)
        {
            view.FontFamily = new FontFamily(font);
        }
        else
        {
            view.ClearValue(Controls.TerminalView.FontFamilyProperty);
        }

        view.InvalidateVisual();
    }

    private void SetAppearance(TerminalTheme? theme = null, string? font = null, bool defaultFont = false, double? size = null) =>
        _settings.SetAppearance(theme ?? _settings.TerminalTheme, defaultFont ? null : font ?? _settings.TerminalFont, size ?? _settings.TerminalFontSize);

    private void AddAppearanceItems(NativeMenu view)
    {
        view.Add(Item(Strings.MenuBiggerText, Shortcut(Key.OemPlus), () => SetAppearance(size: _settings.TerminalFontSize + 1)));
        view.Add(Item(Strings.MenuSmallerText, Shortcut(Key.OemMinus), () => SetAppearance(size: _settings.TerminalFontSize - 1)));
        view.Add(Item(Strings.MenuActualSize, Shortcut(Key.D0), () => SetAppearance(size: 14)));
        var themes = new NativeMenu();
        foreach (var (theme, label) in new[] { (TerminalTheme.System, Strings.MenuThemeSystem), (TerminalTheme.Dark, Strings.MenuThemeDark), (TerminalTheme.Light, Strings.MenuThemeLight) })
        {
            var item = Item(label, null, () => SetAppearance(theme: theme));
            item.ToggleType = MenuItemToggleType.Radio;
            item.IsChecked = theme == _settings.TerminalTheme;
            _themeItems[theme] = item;
            themes.Add(item);
        }

        view.Add(new NativeMenuItem(Strings.MenuTheme) { Menu = themes });
        _fontMenu = new NativeMenu();
        _fontMenu.Opening += (_, _) => FillFontMenu();
        view.Add(new NativeMenuItem(Strings.MenuFont) { Menu = _fontMenu });
        view.Add(new NativeMenuItemSeparator());
        var notifications = Item(Strings.MenuNotifications, null, () => { });
        notifications.ToggleType = MenuItemToggleType.CheckBox;
        notifications.IsChecked = _settings.Notifications;
        notifications.Click += (_, _) =>
        {
            _settings.SetNotifications(!_settings.Notifications);
            notifications.IsChecked = _settings.Notifications;
        };
        view.Add(notifications);
    }

    // Listed when the menu opens, so fonts installed meanwhile show up.
    private void FillFontMenu()
    {
        var menu = _fontMenu!;
        menu.Items.Clear();
        var standard = Item(Strings.MenuFontDefault, null, () => SetAppearance(defaultFont: true));
        standard.ToggleType = MenuItemToggleType.Radio;
        standard.IsChecked = _settings.TerminalFont is null;
        menu.Add(standard);
        menu.Add(new NativeMenuItemSeparator());
        foreach (var family in MonospaceFonts.Installed())
        {
            var item = Item(family, null, () => SetAppearance(font: family));
            item.ToggleType = MenuItemToggleType.Radio;
            item.IsChecked = string.Equals(family, _settings.TerminalFont, StringComparison.OrdinalIgnoreCase);
            menu.Add(item);
        }
    }
}
