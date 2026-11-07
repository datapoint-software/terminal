using Avalonia.Controls;
using Avalonia.Input;
using Datapoint.Terminal.Agent.Projects;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Shell;

public sealed partial class MainWindow
{
    private readonly Dictionary<KeyGesture, Action> _shortcuts = [];

    private static bool IsMac => OperatingSystem.IsMacOS();

    // Elsewhere, shortcuts add Shift to Ctrl: Ctrl with a letter alone belongs to the shell (Ctrl+O, Ctrl+T and Ctrl+W all mean
    // something there).
    private static KeyGesture Shortcut(Key key, bool shift = false) => IsMac
        ? new KeyGesture(key, KeyModifiers.Meta | (shift ? KeyModifiers.Shift : KeyModifiers.None))
        : new KeyGesture(key, KeyModifiers.Control | KeyModifiers.Shift);

    private NativeMenu BuildMenu()
    {
        var file = new NativeMenu();
        file.Add(Item(Strings.MenuNewProject, Shortcut(Key.N, shift: true), () => _ = _windows.PickAsync(this, isNew: true)));
        file.Add(Item(Strings.MenuOpenProject, Shortcut(Key.O), () => _ = _windows.PickAsync(this, isNew: false)));
        FillRecentMenu();
        file.Add(new NativeMenuItem(Strings.MenuOpenRecent) { Menu = _recentMenu });
        file.Add(new NativeMenuItemSeparator());
        if (!Project.IsDefault)
        {
            file.Add(Item(Strings.MenuProjectTrust, null, () => _ = ReviewTrustAsync(always: true)));
            file.Add(new NativeMenuItemSeparator());
        }
        if (!IsMac)
        {
            file.Add(Item(Strings.MenuSettings, new KeyGesture(Key.OemComma, KeyModifiers.Control), _windows.OpenSettings));
            file.Add(new NativeMenuItemSeparator());
        }

        file.Add(Item(Strings.MenuCloseWindow, IsMac ? Shortcut(Key.W, shift: true) : null, Close));

        var terminal = new NativeMenu();
        terminal.Add(Item(Strings.MenuNewTab, Shortcut(Key.T), () => _ = OpenLocalTabAsync()));
        terminal.Add(Item(Strings.MenuCloseTab, Shortcut(Key.W), () => CloseSelectedTab()));
        terminal.Add(new NativeMenuItemSeparator());
        terminal.Add(Item(Strings.MenuNextTab, new KeyGesture(Key.Tab, KeyModifiers.Control), () => MoveTab(1)));
        terminal.Add(Item(Strings.MenuPreviousTab, new KeyGesture(Key.Tab, KeyModifiers.Control | KeyModifiers.Shift), () => MoveTab(-1)));
        return [new NativeMenuItem(Strings.MenuFile) { Menu = file }, new NativeMenuItem(Strings.MenuTerminal) { Menu = terminal }];
    }

    private void FillRecentMenu()
    {
        var menu = _recentMenu;
        menu.Items.Clear();
        foreach (var folder in _windows.Recent)
        {
            menu.Add(Item(ShortPath(folder), gesture: null, () => _windows.Open(folder)));
        }

        if (_windows.Recent.Count > 0)
        {
            menu.Add(new NativeMenuItemSeparator());
        }

        var clear = Item(Strings.MenuClearRecent, gesture: null, _windows.ClearRecent);
        clear.IsEnabled = _windows.Recent.Count > 0;
        menu.Add(clear);
    }

    private NativeMenuItem Item(string header, KeyGesture? gesture, Action action)
    {
        var item = new NativeMenuItem(header) { Gesture = gesture };
        item.Click += (_, _) => action();
        if (gesture is not null && !IsMac && !_shortcuts.ContainsKey(gesture))
        {
            _shortcuts[gesture] = action;
        }

        return item;
    }

    // Outside macOS the menu is drawn in the window and has no keys of its own, so shortcuts are caught here, before the terminal
    // takes them. On macOS the native menu handles them; catching them here too would run them twice.
    private void OnShortcutKeyDown(object? sender, KeyEventArgs e)
    {
        var action = _shortcuts.FirstOrDefault(shortcut => shortcut.Key.Matches(e)).Value;
        if (action is null)
        {
            return;
        }

        e.Handled = true;
        action();
    }

    private static string ShortPath(string folder)
    {
        var home = ProjectFolders.Home;
        return folder.StartsWith(home + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? "~" + folder[home.Length..] : folder;
    }
}
