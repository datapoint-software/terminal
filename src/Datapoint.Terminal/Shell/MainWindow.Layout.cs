using Avalonia.Controls;
using Avalonia.Input;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Shell;

// Product decision 104: the chat fills the window and the terminals dock beside it, on a side set for the application and
// changeable per window. A shortcut hides and shows them, and they come back whenever the agent is about to act in them.
public sealed partial class MainWindow
{
    // About 80 columns at the default text size, which full-screen programs expect.
    private const double DefaultTerminalWidth = 640;
    private const double DefaultTerminalHeight = 300;

    private TerminalSide _terminalSide;
    private bool _areTerminalsHidden;
    private double _terminalWidth = DefaultTerminalWidth;
    private double _terminalHeight = DefaultTerminalHeight;

    private static bool IsBeside(TerminalSide side) => DockLayout.IsBeside(side);

    private void PlaceTerminals(TerminalSide side)
    {
        RememberTerminalSize();
        _terminalSide = side;
        LayOutPanes();
    }

    private void ToggleTerminals()
    {
        RememberTerminalSize();
        _areTerminalsHidden = !_areTerminalsHidden;
        LayOutPanes();
        if (_areTerminalsHidden)
        {
            Chat.FocusInput();
            return;
        }

        SelectedTab?.View.Focus();
    }

    // The user sees everything the agent types, so hidden terminals come back when it asks to run a command or runs one.
    // Focus stays where it is.
    private void ShowTerminalsForAgent()
    {
        if (!_areTerminalsHidden)
        {
            return;
        }

        _areTerminalsHidden = false;
        LayOutPanes();
    }

    // The size the user dragged the pane to is kept per direction, so moving it between sides doesn't lose it.
    private void RememberTerminalSize()
    {
        if (_areTerminalsHidden || TerminalArea.Bounds.Width <= 0)
        {
            return;
        }

        if (IsBeside(_terminalSide))
        {
            _terminalWidth = TerminalArea.Bounds.Width;
            return;
        }

        _terminalHeight = TerminalArea.Bounds.Height;
    }

    private void LayOutPanes()
    {
        new DockLayout(Body, Chat, TerminalArea, ChatSeparator, ChatSplitter).Apply(_terminalSide, _areTerminalsHidden, _terminalWidth, _terminalHeight);
        UpdateLayoutMenu();
    }

    // The agent's commands lead to their step in the chat; the user's own failed ones offer the agent's help.
    private void OnCommandMarkClicked(TerminalTab tab, Emulator.ShellIntegration.CommandRecord command)
    {
        if (ShowChatStep(tab, command.CommandLine) || command.ExitCode is null or 0)
        {
            return;
        }

        FixCommand(tab, command);
    }

    private bool ShowChatStep(TerminalTab tab, string? commandLine)
    {
        if (commandLine is null || _chat.FindCommand(tab.Name, commandLine) is not { } item)
        {
            return false;
        }

        Chat.Reveal(item);
        return true;
    }

    private readonly Dictionary<TerminalSide, NativeMenuItem> _sideItems = [];
    private NativeMenuItem? _toggleTerminalsItem;

    private NativeMenu BuildViewMenu()
    {
        var view = new NativeMenu();
        _toggleTerminalsItem = Item(Strings.MenuHideTerminals, Shortcut(Key.J), ToggleTerminals);
        view.Add(_toggleTerminalsItem);
        view.Add(SidebarMenuItem());
        view.Add(ShowMarkdownMenuItem());
        view.Add(new NativeMenuItemSeparator());
        foreach (var (side, label) in new[]
        {
            (TerminalSide.Right, Strings.MenuTerminalsRight),
            (TerminalSide.Left, Strings.MenuTerminalsLeft),
            (TerminalSide.Bottom, Strings.MenuTerminalsBottom),
        })
        {
            var item = Item(label, null, () => PlaceTerminals(side));
            item.ToggleType = MenuItemToggleType.Radio;
            _sideItems[side] = item;
            view.Add(item);
        }

        view.Add(new NativeMenuItemSeparator());
        view.Add(Item(Strings.MenuTerminalsSideDefault, null, () => _settings.SetTerminalSide(_terminalSide)));
        view.Add(new NativeMenuItemSeparator());
        AddAppearanceItems(view);
        return view;
    }

    private void UpdateLayoutMenu()
    {
        _toggleTerminalsItem?.Header = _areTerminalsHidden ? Strings.MenuShowTerminals : Strings.MenuHideTerminals;
        foreach (var (side, item) in _sideItems)
        {
            item.IsChecked = side == _terminalSide;
        }
    }
}
