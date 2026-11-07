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
    private const double DefaultDocumentLength = 280;

    private TerminalSide _terminalSide;
    private bool _areTerminalsHidden;
    private double _terminalWidth = DefaultTerminalWidth;
    private double _terminalHeight = DefaultTerminalHeight;
    private double? _documentSize;

    private static bool IsBeside(TerminalSide side) => DockLayout.IsBeside(side);

    private void PlaceTerminals(TerminalSide side)
    {
        RememberTerminalSize();
        RememberDocumentSize();
        if (IsBeside(side) != IsBeside(_terminalSide))
        {
            _documentSize = null;
        }

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
        if (_areTerminalsHidden || DockedArea.Bounds.Width <= 0)
        {
            return;
        }

        if (IsBeside(_terminalSide))
        {
            _terminalWidth = DockedArea.Bounds.Width;
            return;
        }

        _terminalHeight = DockedArea.Bounds.Height;
    }

    private void LayOutPanes()
    {
        new DockLayout(Body, Chat, DockedArea, ChatSeparator, ChatSplitter).Apply(_terminalSide, _areTerminalsHidden, _terminalWidth, _terminalHeight);
        LayOutDocuments();
        UpdateLayoutMenu();
    }

    // Product decision 108: documents get a pane of their own, below the terminals, or beside them when the terminals are below
    // the chat, so neither hides the other. It's there only while a document is open, and keeps the size the user dragged it to.
    private void LayOutDocuments()
    {
        RememberDocumentSize();
        var size = _documentSize ?? DefaultDocumentSize();
        var side = IsBeside(_terminalSide) ? TerminalSide.Bottom : TerminalSide.Right;
        new DockLayout(DockedArea, TerminalArea, DocumentArea, DocumentSeparator, DocumentSplitter).Apply(side, _documents.Count == 0, size, size);
    }

    private void RememberDocumentSize()
    {
        if (!DocumentArea.IsVisible || DocumentArea.Bounds.Width <= 0)
        {
            return;
        }

        _documentSize = IsBeside(_terminalSide) ? DocumentArea.Bounds.Height : DocumentArea.Bounds.Width;
    }

    // Two fifths of the terminals' column at first, leaving the terminal the larger share.
    private double DefaultDocumentSize()
    {
        var length = IsBeside(_terminalSide) ? DockedArea.Bounds.Height : DockedArea.Bounds.Width;
        return length > 0 ? Math.Max(DockLayout.MinimumSize, length * 0.4) : DefaultDocumentLength;
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
        view.Add(Item(Strings.MenuNextArea, new KeyGesture(Key.F6), () => MoveAreaFocus(1)));
        view.Add(Item(Strings.MenuPreviousArea, new KeyGesture(Key.F6, KeyModifiers.Shift), () => MoveAreaFocus(-1)));
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

    // F6 moves the keyboard between the window's areas, as in most desktop applications: the chat, the terminals and the
    // documents, skipping any that are hidden.
    private void MoveAreaFocus(int step)
    {
        var areas = new List<(Control Area, Action Focus)> { (Chat, Chat.FocusInput) };
        if (TerminalArea.IsEffectivelyVisible && SelectedTab is { } tab)
        {
            areas.Add((TerminalArea, () => tab.View.Focus()));
        }

        if (DocumentArea.IsEffectivelyVisible)
        {
            areas.Add((DocumentArea, FocusShownDocument));
        }

        var current = areas.FindIndex(area => area.Area.IsKeyboardFocusWithin);
        var next = current < 0 ? 0 : (current + step + areas.Count) % areas.Count;
        areas[next].Focus();
    }

    private void FocusShownDocument()
    {
        if (ShownDocument is { } shown)
        {
            shown.View.FocusText();
            return;
        }

        ShownHostFile?.FocusText();
    }
}
