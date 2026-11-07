using Avalonia.Controls;
using Datapoint.Terminal.Agent.Projects;
using Datapoint.Terminal.Agent.Projects.Ignore;
using Datapoint.Terminal.Agent.Runbooks;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Shell;

// Product decision 46: runbooks. The menu writes the request into the chat for the user to complete and send, so running or
// saving one is always the user's message, with any parameters in their words.
public sealed partial class MainWindow
{
    private NativeMenu? _runbookMenu;

    private RunbookFolder CreateRunbooks() => new(
        Path.Combine(Project.Folder, "runbooks"),
        path => TerminalIgnore.IsIgnored(path, Project.Folder, ProjectFolders.Home),
        CanWrite: !Project.IsReadOnly,
        IsTrusted: () => Project.IsDefault || _trust is { IsTrusted: true });

    private void AddRunbookItems(NativeMenu terminal)
    {
        _runbookMenu = new NativeMenu();
        _runbookMenu.Opening += (_, _) => FillRunbookMenu();
        FillRunbookMenu();
        terminal.Add(new NativeMenuItem(Strings.MenuRunRunbook) { Menu = _runbookMenu });
        terminal.Add(Item(Strings.MenuSaveRunbook, null, () => DraftInChat(Strings.RunbookSaveDraft)));
    }

    // Listed when the menu opens, so runbooks the agent just saved show up.
    private void FillRunbookMenu()
    {
        var menu = _runbookMenu!;
        menu.Items.Clear();
        var names = CreateRunbooks().Names();
        foreach (var name in names)
        {
            menu.Add(Item(name, null, () => DraftInChat(Strings.Format(Strings.RunbookRunDraft, name, SelectedTab?.Name ?? Strings.TabLocal))));
        }

        if (names.Count > 0)
        {
            return;
        }

        var none = Item(Strings.MenuNoRunbooks, null, () => { });
        none.IsEnabled = false;
        menu.Add(none);
    }

    private void DraftInChat(string text)
    {
        if (_isChatHidden)
        {
            ToggleChat();
        }

        _chat.Draft = text;
        Chat.FocusInput();
    }
}
