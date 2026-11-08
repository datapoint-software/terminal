using Avalonia.Controls;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Threading;
using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Agent.Files;
using Datapoint.Terminal.Agent.Workspaces;
using Datapoint.Terminal.Controls;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Sessions;
using Datapoint.Terminal.Sessions.Local;

namespace Datapoint.Terminal.Shell;

public sealed partial class MainWindow
{
    private readonly ObservableCollection<TerminalTab> _tabs = [];

    public ObservableCollection<TerminalTab> TabList => _tabs;

    private TerminalTab? SelectedTab => Tabs.SelectedItem as TerminalTab;

    private async Task<TerminalTab?> OpenLocalTabAsync(bool select = true)
    {
        var view = new TerminalView { Padding = new Thickness(12, 10), IsVisible = false };
        var tab = new TerminalTab(NextTabName(Strings.TabLocal), view, RequestClose) { ConnectionName = Strings.TabLocal };
        try
        {
            var options = Workspace.IsDefault ? new LocalSessionOptions() : new LocalSessionOptions { WorkingDirectory = Workspace.Folder };
            var session = await LocalSession.StartAsync(options, CancellationToken.None).ConfigureAwait(true);
            var files = OperatingSystem.IsWindows() ? null : new LocalHostFiles(WorkspaceFolders.Home);
            Attach(tab, Connect(tab, session), environment: null, select, files);
            return tab;
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or PlatformNotSupportedException or System.ComponentModel.Win32Exception)
        {
            _chat.AddNotice(Strings.Format(Strings.NoticeShellFailed, exception.Message));
            return null;
        }
    }

    // A tab the agent opened isn't selected: the agent never switches the tab the user is looking at.
    private void Attach(TerminalTab tab, TerminalConnection connection, string? environment, bool select = true, IHostFiles? files = null, string? place = null)
    {
        tab.Connection = connection;
        tab.AgentTerminal = new AgentTerminal(tab.Name, new ConnectionTerminal(connection), environment)
        {
            IsRemote = tab.IsRemote,
            Connection = tab.ConnectionName,
            Files = files,
            Place = place,
        };
        StartRecording(tab, connection);
        connection.OutputArrived += (_, _) => tab.View.NotifyOutput();
        tab.View.CommandMarkClicked += (_, e) => OnCommandMarkClicked(tab, e.Command);
        tab.View.ContextFlyout = BuildTerminalContextMenu(tab);
        tab.View.GotFocus += (_, _) => SelectPane(tab);
        ApplyAppearance(tab);
        connection.TitleChanged += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            tab.ShellTitle = connection.Terminal.Title;
            UpdateTitle();
        });
        connection.Ended += (_, _) => Dispatcher.UIThread.Post(() => CloseTab(tab));
        tab.View.Input += (_, e) =>
        {
            _chat.OnUserTyped(tab.AgentTerminal);
            connection.Send(e.Data);
        };
        tab.View.Resized += (_, e) => _ = connection.ResizeAsync(e.Columns, e.Rows, CancellationToken.None);
        tab.View.Terminal = connection.Terminal;
        TerminalHost.Children.Add(tab.View);
        _tabs.Add(tab);
        NoTerminals.IsVisible = false;
        _agent?.AddTerminal(tab.AgentTerminal);
        if (!select && _tabs.Count > 1)
        {
            return;
        }

        Tabs.SelectedItem = tab;
    }

    private string NextTabName(string baseName)
    {
        var taken = _tabs.Select(tab => tab.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(baseName))
        {
            return baseName;
        }

        var number = 2;
        while (taken.Contains($"{baseName} {number}"))
        {
            number++;
        }

        return $"{baseName} {number}";
    }

    private void ShowSelectedTab()
    {
        var selected = SelectedTab;
        LayOutPanes(selected);

        _chat.ShowTerminal(selected?.AgentTerminal);
        UpdateTitle();

        // Arrow keys in the tab list move between tabs; otherwise typing goes straight to the terminal.
        if (selected is null || Tabs.IsKeyboardFocusWithin)
        {
            return;
        }

        selected.View.Focus();
    }

    private void MoveTab(int step)
    {
        if (_tabs.Count < 2 || SelectedTab is not { } selected)
        {
            return;
        }

        var index = (_tabs.IndexOf(selected) + step + _tabs.Count) % _tabs.Count;
        Tabs.SelectedItem = _tabs[index];
        _tabs[index].View.Focus();
    }

    private void CloseSelectedTab()
    {
        if (SelectedTab is not { } tab)
        {
            return;
        }

        RequestClose(tab);
    }

    private void RequestClose(TerminalTab tab) => _ = RequestCloseAsync(tab);

    // Asks first when closing would cut something off; a tab whose shell has already ended closes without asking.
    private async Task RequestCloseAsync(TerminalTab tab)
    {
        if (tab.BusyReason is { } reason)
        {
            var choice = await ChoiceWindow.AskAsync(
                this,
                Strings.Format(Strings.CloseTabTitle, tab.Name),
                Strings.Format(Strings.CloseTabMessage, tab.Label, reason),
                Strings.Cancel,
                Strings.CloseTabConfirm).ConfigureAwait(true);
            if (choice != 1)
            {
                return;
            }
        }

        CloseTab(tab);
    }

    // Focus goes to its button, so the keyboard isn't left on a closed terminal, unless it's in a document or the plan.
    private void ShowNoTerminals()
    {
        NoTerminals.IsVisible = true;
        if (FocusedPane is not null || PlanArea.IsKeyboardFocusWithin)
        {
            return;
        }

        OpenTerminalButton.Focus();
    }

    private void CloseTab(TerminalTab tab)
    {
        var index = _tabs.IndexOf(tab);
        if (index < 0)
        {
            return;
        }

        var panes = tab.Group?.Tabs.ToList() ?? [];
        var pane = panes.IndexOf(tab);
        panes.Remove(tab);
        PaneGroup.Leave(tab);
        _ = DisposeTabAsync(tab);
        if (_tabs.Count == 0)
        {
            ShowNoTerminals();
            return;
        }

        // A closed pane's neighbour takes its place, so the rest of the split stays in view.
        Tabs.SelectedItem = panes.Count > 0 ? panes[Math.Min(pane, panes.Count - 1)] : _tabs[Math.Min(index, _tabs.Count - 1)];
        SelectedTab?.View.Focus();
    }

    // Closing a tab ends its shell; the agent stops first if it's working there, so it never acts on a terminal that's gone.
    private async Task DisposeTabAsync(TerminalTab tab)
    {
        if (!_tabs.Remove(tab))
        {
            return;
        }

        if (tab.Activity != TabActivity.None)
        {
            _agent?.Interrupt();
        }

        if (tab.AgentTerminal is { } agentTerminal)
        {
            _agent?.RemoveTerminal(agentTerminal);
        }

        TerminalHost.Children.Remove(tab.View);
        SaveScrollback(tab);
        foreach (var forward in tab.Forwards)
        {
            await forward.DisposeAsync().ConfigureAwait(true);
        }

        if (tab.Connection is not { } connection)
        {
            return;
        }

        await connection.DisposeAsync().ConfigureAwait(true);
        StopRecording(tab);
    }

    private void OnAgentEvent(object? sender, AgentEventArgs e)
    {
        if (e.Kind != AgentEventKind.StatusChanged)
        {
            return;
        }

        var activity = e.Status switch
        {
            AgentStatus.RunningCommand => TabActivity.AgentActing,
            AgentStatus.WaitingForApproval or AgentStatus.WaitingForUser => TabActivity.AgentNeedsUser,
            _ => TabActivity.None,
        };
        Dispatcher.UIThread.Post(() =>
        {
            foreach (var tab in _tabs)
            {
                tab.Activity = string.Equals(tab.Name, e.Terminal, StringComparison.OrdinalIgnoreCase) ? activity : TabActivity.None;
            }
        });
    }

    private bool _isCloseConfirmed;

    // The tabs that closing would cut off, one line each, for the question asked before closing a window or quitting.
    public IReadOnlyList<string> BusyTabs(bool withWorkspace) => [.. _tabs
        .Select(tab => (label: tab.Label, reason: tab.BusyReason))
        .Concat(UnsavedHostFiles.Select(file => (label: file.Name, reason: (string?)Strings.HostFileUnsavedReason)))
        .Where(item => item.reason is not null)
        .Select(item => Strings.Format(Strings.CloseTabLine, withWorkspace ? $"{Workspace.Name} › {item.label}" : item.label, item.reason))];

    // Set when quitting was confirmed for all windows at once.
    public void AllowClose() => _isCloseConfirmed = true;

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        var busy = BusyTabs(withWorkspace: false);
        if (_isCloseConfirmed || busy.Count == 0)
        {
            return;
        }

        e.Cancel = true;
        _ = ConfirmCloseAsync(string.Join(Environment.NewLine, busy));
    }

    private async Task ConfirmCloseAsync(string details)
    {
        var choice = await ChoiceWindow.AskAsync(this, Strings.CloseWindowTitle, Strings.CloseWindowMessage, details, hasDefault: true, Strings.Cancel, Strings.CloseWindowConfirm).ConfigureAwait(true);
        if (choice != 1)
        {
            return;
        }

        _isCloseConfirmed = true;
        Close();
    }

    private void ShowTerminalCount() => TerminalsSummary.Text = OpenCount(_tabs.Count);

    internal static string OpenCount(int count) => count switch
    {
        0 => Strings.CountNoneOpen,
        1 => Strings.CountOneOpen,
        _ => Strings.Format(Strings.CountOpen, count),
    };
}
