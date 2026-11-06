using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Threading;
using Datapoint.Terminal.Agent;
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

    private async Task OpenLocalTabAsync()
    {
        var view = new TerminalView { Padding = new Thickness(12, 10), IsVisible = false };
        var tab = new TerminalTab(NextTabName(Strings.TabLocal), view, CloseTab);
        try
        {
            var options = Project.IsDefault ? new LocalSessionOptions() : new LocalSessionOptions { WorkingDirectory = Project.Folder };
            var session = await LocalSession.StartAsync(options, CancellationToken.None).ConfigureAwait(true);
            Attach(tab, new TerminalConnection(session, 80, 24), environment: null, _settings.Current.DefaultApprovalMode);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or PlatformNotSupportedException or System.ComponentModel.Win32Exception)
        {
            _chat.AddNotice(Strings.Format(Strings.NoticeShellFailed, exception.Message));
        }
    }

    private void Attach(TerminalTab tab, TerminalConnection connection, string? environment, ApprovalMode defaultMode)
    {
        tab.Connection = connection;
        tab.AgentTerminal = new AgentTerminal(tab.Name, new ConnectionTerminal(connection), defaultMode, environment);
        connection.OutputArrived += (_, _) => tab.View.NotifyOutput();
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
        _agent?.AddTerminal(tab.AgentTerminal);
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
        foreach (var tab in _tabs)
        {
            tab.View.IsVisible = ReferenceEquals(tab, selected);
        }

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

        CloseTab(tab);
    }

    private void CloseTab(TerminalTab tab)
    {
        var index = _tabs.IndexOf(tab);
        if (index < 0)
        {
            return;
        }

        _ = DisposeTabAsync(tab);
        if (_tabs.Count == 0)
        {
            Close();
            return;
        }

        Tabs.SelectedItem = _tabs[Math.Min(index, _tabs.Count - 1)];
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
        if (tab.Connection is not { } connection)
        {
            return;
        }

        await connection.DisposeAsync().ConfigureAwait(true);
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
}
