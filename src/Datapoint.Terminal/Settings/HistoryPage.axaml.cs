using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Datapoint.Terminal.Agent.Workspaces.History;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Shell;

namespace Datapoint.Terminal.Settings;

// Settings › History (product decision 52): how long history is kept, for every workspace, and clearing it per connection in
// the workspace whose window Settings was opened from.
public sealed partial class HistoryPage : UserControl
{
    private IHistoryAccess? _access;
    private bool _isLoading;

    public HistoryPage()
    {
        InitializeComponent();
        RetentionBox.ItemsSource = HistoryWindow.s_retention.Select(HistoryWindow.RetentionLabel).ToList();
        RetentionBox.SelectionChanged += (_, _) => _ = ChangeRetentionAsync();
    }

    public IHistoryAccess? Access
    {
        get => _access;
        set
        {
            _access = value;
            Fill();
        }
    }

    private void Fill()
    {
        _isLoading = true;
        RetentionBox.IsEnabled = _access is not null;
        RetentionBox.SelectedIndex = Math.Max(0, Array.IndexOf(HistoryWindow.s_retention, _access?.RetentionDays));
        _isLoading = false;
        Connections.Children.Clear();
        WorkspaceHeading.Text = _access is null ? string.Empty : Strings.Format(Strings.HistoryInWorkspace, _access.WorkspaceName);
        IReadOnlyList<ConnectionUsage> usage = [];
        Try(() => usage = _access?.Usage() ?? []);
        foreach (var item in usage)
        {
            if (Connections.Children.Count > 0)
            {
                Connections.Children.Add(new Rectangle { Classes = { "hairline" } });
            }

            Connections.Children.Add(Row(item));
        }

        var isEmpty = usage.Count == 0;
        ConnectionsGroup.IsVisible = !isEmpty;
        ClearHint.IsVisible = !isEmpty;
        Status.Text = _access is null ? Strings.HistoryNoWorkspace : isEmpty ? Strings.HistoryNothingKept : Status.Text;
    }

    private Grid Row(ConnectionUsage item)
    {
        var row = new Grid { Classes = { "row" }, ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        row.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = item.Connection, VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = Strings.Format(Strings.HistoryUsage, item.Conversations, Size(item.Characters)), Classes = { "secondary" }, VerticalAlignment = VerticalAlignment.Center },
            },
        });
        var clear = new Button { Classes = { "plain", "danger" }, Content = Strings.HistoryClearRow };
        Avalonia.Automation.AutomationProperties.SetName(clear, Strings.Format(Strings.HistoryClearRowName, item.Connection));
        clear.Click += (_, _) => _ = ClearAsync(item.Connection);
        Grid.SetColumn(clear, 1);
        row.Children.Add(clear);
        return row;
    }

    // Characters stand in for bytes: most terminal text is ASCII, and this is a rough guide to what clearing frees.
    private static string Size(long characters) => characters switch
    {
        < 1024 * 1024 => string.Format(CultureInfo.CurrentCulture, "{0:0} KB", Math.Max(1, characters / 1024.0)),
        _ => string.Format(CultureInfo.CurrentCulture, "{0:0.0} MB", characters / (1024.0 * 1024)),
    };

    private async Task ClearAsync(string connection)
    {
        if (_access is null || TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var answer = await ChoiceWindow.AskAsync(
            owner,
            Strings.Format(Strings.HistoryClearOneTitle, connection),
            Strings.Format(Strings.HistoryClearOneMessage, connection),
            details: null,
            hasDefault: false,
            Strings.Cancel,
            Strings.HistoryClearRow.TrimEnd('…')).ConfigureAwait(true);
        if (answer != 1)
        {
            return;
        }

        Try(() => _access.Clear(connection));
        Status.Text = Strings.Format(Strings.HistoryClearedConnection, connection);
        Fill();
    }

    // Keeping less deletes what's older at once, so it's confirmed first.
    private async Task ChangeRetentionAsync()
    {
        if (_isLoading || _access is null || RetentionBox.SelectedIndex < 0 || TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var days = HistoryWindow.s_retention[RetentionBox.SelectedIndex];
        var current = _access.RetentionDays;
        if (days == current)
        {
            return;
        }

        if (days is not null && (current is null || days < current)
            && await ChoiceWindow.AskAsync(owner, Strings.HistoryShortenTitle, Strings.Format(Strings.HistoryShortenMessage, HistoryWindow.RetentionLabel(days)), details: null, hasDefault: false, Strings.Cancel, Strings.HistoryShortenConfirm).ConfigureAwait(true) != 1)
        {
            Fill();
            return;
        }

        Try(() => _access.SetRetentionDays(days));
        Fill();
    }

    private void Try(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException exception)
        {
            Status.Text = exception.Message;
        }
    }
}
