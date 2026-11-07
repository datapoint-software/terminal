using Avalonia.Controls;
using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Emulator.ShellIntegration;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Shell;

// Product.md, Explain and fix: the user picks part of a terminal and the chat gets a draft request with that part attached.
// Nothing is sent until the user sends it, and the attachment travels as terminal output, never as the user's words.
public sealed partial class MainWindow
{
    // The longest excerpt kept; the agent's own limit then cuts long output to its start and end.
    private const int MaxExcerptLines = 400;

    private void AddExplainItems(NativeMenu terminal)
    {
        terminal.Add(Item(Strings.MenuExplainSelection, null, () => ExplainSelection(SelectedTab)));
        terminal.Add(Item(Strings.MenuFixFailedCommand, null, () => FixLastFailedCommand(SelectedTab)));
    }

    private ContextMenu BuildTerminalContextMenu(TerminalTab tab)
    {
        var explain = new MenuItem { Header = Strings.MenuExplainSelection };
        explain.Click += (_, _) => ExplainSelection(tab);
        var fix = new MenuItem { Header = Strings.MenuFixFailedCommand };
        fix.Click += (_, _) => FixLastFailedCommand(tab);
        var menu = new ContextMenu { ItemsSource = new[] { explain, fix } };
        menu.Opening += (_, _) =>
        {
            explain.IsEnabled = tab.View.HasSelection;
            fix.IsEnabled = LastFailedCommand(tab) is not null;
        };
        return menu;
    }

    private void ExplainSelection(TerminalTab? tab)
    {
        if (tab is null || !tab.View.HasSelection)
        {
            _chat.AddNotice(Strings.NoticeSelectFirst);
            return;
        }

        Attach(new TerminalExcerpt(tab.Name, Strings.ExcerptSelection, LastLines(tab.View.SelectedText)), Strings.ExplainSelectionDraft);
    }

    private void FixLastFailedCommand(TerminalTab? tab)
    {
        if (tab is null || LastFailedCommand(tab) is not { } command)
        {
            _chat.AddNotice(Strings.NoticeNoFailedCommand);
            return;
        }

        FixCommand(tab, command);
    }

    // A failed command's mark that has no chat step (the user ran it) offers the same, since there's nothing to show.
    private void FixCommand(TerminalTab tab, CommandRecord command)
    {
        var terminal = tab.Connection!.Terminal;
        var first = command.InputLine ?? command.PromptLine;
        var text = terminal.GetLineText(first, command.EndLine ?? first);
        Attach(new TerminalExcerpt(tab.Name, Strings.Format(Strings.ExcerptFailedCommand, command.ExitCode), LastLines(text)), Strings.FixCommandDraft);
    }

    private static CommandRecord? LastFailedCommand(TerminalTab tab)
    {
        if (tab.Connection?.Terminal is not { } terminal)
        {
            return null;
        }

        lock (terminal.SyncRoot)
        {
            return terminal.Commands.LastOrDefault(command => command.IsFinished && command.ExitCode is not (null or 0));
        }
    }

    private void Attach(TerminalExcerpt excerpt, string draft)
    {
        _chat.Excerpt = excerpt;
        DraftInChat(draft);
    }

    private static string LastLines(string text)
    {
        var lines = text.Split('\n');
        return lines.Length <= MaxExcerptLines ? text : string.Join('\n', lines[^MaxExcerptLines..]);
    }
}
