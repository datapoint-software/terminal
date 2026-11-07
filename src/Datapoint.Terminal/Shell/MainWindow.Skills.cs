using Avalonia.Controls;
using Datapoint.Terminal.Agent.Skills;
using Datapoint.Terminal.Agent.Workspaces;
using Datapoint.Terminal.Agent.Workspaces.Ignore;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Shell;

// Product decision 103: skills. The menu writes the request into the chat for the user to complete and send, so running or
// saving one is always the user's message, in their words.
public sealed partial class MainWindow
{
    private NativeMenu? _skillMenu;

    private SkillFolder CreateSkills() => new(
        Path.Combine(Workspace.Folder, "skills"),
        path => TerminalIgnore.IsIgnored(path, Workspace.Folder, WorkspaceFolders.Home),
        CanWrite: !Workspace.IsReadOnly,
        IsTrusted: () => Workspace.IsDefault || _trust is { IsTrusted: true })
    {
        Reviewed = ReviewedSkill,
    };

    private void AddSkillItems(NativeMenu terminal)
    {
        _skillMenu = new NativeMenu();
        _skillMenu.Opening += (_, _) => FillSkillMenu();
        FillSkillMenu();
        terminal.Add(new NativeMenuItem(Strings.MenuRunSkill) { Menu = _skillMenu });
        terminal.Add(Item(Strings.MenuSaveSkill, null, () => DraftInChat(Strings.SkillSaveDraft)));
    }

    // Listed when the menu opens, so skills the agent just saved show up.
    private void FillSkillMenu()
    {
        var menu = _skillMenu!;
        menu.Items.Clear();
        var skills = CreateSkills().List();
        foreach (var skill in skills)
        {
            menu.Add(Item(skill.Title, null, () => DraftInChat(Strings.Format(Strings.SkillRunDraft, skill.Title))));
        }

        if (skills.Count > 0)
        {
            return;
        }

        var none = Item(Strings.MenuNoSkills, null, () => { });
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
