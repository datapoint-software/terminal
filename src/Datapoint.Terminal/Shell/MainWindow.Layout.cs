using Avalonia.Controls;
using Avalonia.Input;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Shell;

// Product decisions 17 and 71: the chat pane docks to any side, set for the application and changeable per window, and a
// shortcut hides and shows it.
public sealed partial class MainWindow
{
    private const double DefaultChatWidth = 380;
    private const double DefaultChatHeight = 300;

    private ChatSide _chatSide;
    private bool _isChatHidden;
    private double _chatWidth = DefaultChatWidth;
    private double _chatHeight = DefaultChatHeight;

    private static bool IsBeside(ChatSide side) => ChatLayout.IsBeside(side);

    private void PlaceChat(ChatSide side)
    {
        RememberChatSize();
        _chatSide = side;
        LayOutChat();
    }

    private void ToggleChat()
    {
        RememberChatSize();
        _isChatHidden = !_isChatHidden;
        LayOutChat();
        if (_isChatHidden)
        {
            SelectedTab?.View.Focus();
            return;
        }

        Chat.FocusInput();
    }

    // The size the user dragged the pane to is kept per direction, so moving it between sides doesn't lose it.
    private void RememberChatSize()
    {
        if (_isChatHidden || Chat.Bounds.Width <= 0)
        {
            return;
        }

        if (IsBeside(_chatSide))
        {
            _chatWidth = Chat.Bounds.Width;
            return;
        }

        _chatHeight = Chat.Bounds.Height;
    }

    private void LayOutChat()
    {
        new ChatLayout(Body, Chat, TerminalArea, ChatSeparator, ChatSplitter).Apply(_chatSide, _isChatHidden, _chatWidth, _chatHeight);
        UpdateChatMenu();
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

        if (_isChatHidden)
        {
            ToggleChat();
        }

        Chat.Reveal(item);
        return true;
    }

    private readonly Dictionary<ChatSide, NativeMenuItem> _sideItems = [];
    private NativeMenuItem? _toggleChatItem;

    private NativeMenu BuildViewMenu()
    {
        var view = new NativeMenu();
        _toggleChatItem = Item(Strings.MenuHideChat, Shortcut(Key.J), ToggleChat);
        view.Add(_toggleChatItem);
        view.Add(new NativeMenuItemSeparator());
        foreach (var (side, label) in new[]
        {
            (ChatSide.Right, Strings.MenuChatRight),
            (ChatSide.Left, Strings.MenuChatLeft),
            (ChatSide.Bottom, Strings.MenuChatBottom),
            (ChatSide.Top, Strings.MenuChatTop),
        })
        {
            var item = Item(label, null, () => PlaceChat(side));
            item.ToggleType = MenuItemToggleType.Radio;
            _sideItems[side] = item;
            view.Add(item);
        }

        view.Add(new NativeMenuItemSeparator());
        view.Add(Item(Strings.MenuChatSideDefault, null, () => _settings.SetChatSide(_chatSide)));
        view.Add(new NativeMenuItemSeparator());
        AddAppearanceItems(view);
        return view;
    }

    private void UpdateChatMenu()
    {
        _toggleChatItem?.Header = _isChatHidden ? Strings.MenuShowChat : Strings.MenuHideChat;
        foreach (var (side, item) in _sideItems)
        {
            item.IsChecked = side == _chatSide;
        }
    }
}
