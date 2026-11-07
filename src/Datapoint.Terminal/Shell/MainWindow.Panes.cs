using Avalonia.Controls;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Shell;

// Product decision 44: split panes. A split opens another terminal to the same connection beside or below the current one; each
// pane is a tab of its own, so the agent and the history see nothing new.
public sealed partial class MainWindow
{
    private PaneLayout? _paneLayout;
    private PaneGroup? _shownGroup;

    private PaneLayout Panes => _paneLayout ??= new PaneLayout(TerminalHost);

    private void LayOutPanes(TerminalTab? selected)
    {
        if (_shownGroup is { Tabs.Count: > 1 } previous)
        {
            previous.Sizes = Panes.Measure();
        }

        var group = selected?.Group;
        IReadOnlyList<TerminalTab> shown = group?.Tabs ?? (selected is null ? [] : [selected]);
        Panes.Apply(
            [.. _tabs.Select(tab => (Control)tab.View)],
            [.. shown.Select(tab => (Control)tab.View)],
            group?.IsBeside ?? true,
            shown.ToList().IndexOf(selected!),
            group?.Sizes);
        _shownGroup = group;
    }

    private async Task SplitAsync(bool beside)
    {
        if (SelectedTab is not { } selected)
        {
            return;
        }

        if (selected.Group is { IsFull: true })
        {
            _chat.AddNotice(Strings.Format(Strings.NoticePanesFull, PaneGroup.MaxPanes));
            return;
        }

        var tab = await OpenLikeAsync(selected).ConfigureAwait(true);

        // The first tab may have closed, or filled up, while the new one was connecting; the new one then stays a tab of its own.
        if (tab is null || !_tabs.Contains(selected) || selected.Group is { IsFull: true })
        {
            if (tab is not null)
            {
                Tabs.SelectedItem = tab;
            }

            return;
        }

        var group = selected.Group ?? new PaneGroup(beside);
        group.IsBeside = beside;
        group.Insert(selected, tab);
        _tabs.Move(_tabs.IndexOf(tab), _tabs.IndexOf(selected) + (_tabs.IndexOf(tab) > _tabs.IndexOf(selected) ? 1 : 0));
        Tabs.SelectedItem = tab;
        tab.View.Focus();
    }

    // A pane to the same place: another local shell, or another terminal on the same SSH connection (logged into again only
    // if that has closed).
    private async Task<TerminalTab?> OpenLikeAsync(TerminalTab tab)
    {
        if (!tab.IsRemote)
        {
            return await OpenLocalTabAsync(select: false).ConfigureAwait(true);
        }

        var connection = Connections.FirstOrDefault(item => string.Equals(item.Name, tab.ConnectionName, StringComparison.OrdinalIgnoreCase));
        if (connection is null)
        {
            _chat.AddNotice(Strings.Format(Strings.NoticePaneConnectionGone, tab.ConnectionName));
            return null;
        }

        return await OpenOnSameConnectionAsync(tab, connection).ConfigureAwait(true)
            ?? await OpenConnectionAsync(connection, select: false).ConfigureAwait(true);
    }

    // Clicking into a pane, or moving focus there, makes it the selected tab, so the chat and the find bar follow it.
    private void SelectPane(TerminalTab tab)
    {
        if (ReferenceEquals(SelectedTab, tab) || !tab.View.IsVisible || !_tabs.Contains(tab))
        {
            return;
        }

        Tabs.SelectedItem = tab;
    }

    private void MovePaneFocus(int step)
    {
        if (SelectedTab is not { Group: { } group } selected)
        {
            return;
        }

        var next = group.Tabs[(group.IndexOf(selected) + step + group.Tabs.Count) % group.Tabs.Count];
        Tabs.SelectedItem = next;
        next.View.Focus();
    }

    private void MovePaneToOwnTab()
    {
        if (SelectedTab is not { Group: not null } selected)
        {
            return;
        }

        PaneGroup.Leave(selected);
        ShowSelectedTab();
        selected.View.Focus();
    }
}
