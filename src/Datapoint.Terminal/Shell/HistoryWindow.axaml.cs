using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Datapoint.Terminal.Agent.Workspaces.History;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Shell;

// Product decision 52: earlier conversations, read-only, with search, the retention setting and clearing.
public sealed partial class HistoryWindow : Window
{
    internal static readonly int?[] s_retention = [null, 30, 90, 365];

    private readonly IHistoryAccess _access;
    private readonly DispatcherTimer _searchDelay = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private bool _isLoading = true;

    public HistoryWindow() : this(null!)
    {
    }

    public HistoryWindow(IHistoryAccess access)
    {
        InitializeComponent();
        _access = access;
        if (access is null)
        {
            return;
        }

        RetentionBox.ItemsSource = s_retention.Select(RetentionLabel).ToList();
        RetentionBox.SelectedIndex = Math.Max(0, Array.IndexOf(s_retention, access.RetentionDays));
        RetentionBox.SelectionChanged += (_, _) => _ = ChangeRetentionAsync();
        SearchBox.TextChanged += (_, _) => { _searchDelay.Stop(); _searchDelay.Start(); };
        _searchDelay.Tick += (_, _) => { _searchDelay.Stop(); Fill(); };
        List.SelectionChanged += (_, _) => ShowSelected();
        ClearButton.Click += (_, _) => _ = ClearAsync();
        CloseButton.Click += (_, _) => Close();
        KeyDown += OnKeyDown;
        Opened += (_, _) => SearchBox.Focus();
        Fill();
        _isLoading = false;
    }

    internal static string RetentionLabel(int? days) => days switch
    {
        null => Strings.HistoryKeepForever,
        365 => Strings.HistoryKeepYear,
        _ => Strings.Format(Strings.HistoryKeepDays, days),
    };

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;
        Close();
    }

    // Each item's tag is the conversation it opens.
    private void Fill()
    {
        var words = SearchBox.Text?.Trim() ?? string.Empty;
        Try(() =>
        {
            var items = words.Length == 0
                ? _access.Conversations().Select(item => Item(item.Id, item.Started, item.Title, Strings.Format(Strings.HistoryEntries, item.Entries)))
                : _access.Search(words).Select(item => Item(item.Conversation, item.Time, item.Text, Kind(item)));
            List.ItemsSource = items.ToList();
            Entries.Children.Clear();
            Status.Text = List.ItemCount == 0 ? (words.Length == 0 ? Strings.HistoryEmpty : Strings.HistoryNoMatches) : string.Empty;
        });
    }

    private static ListBoxItem Item(long conversation, DateTimeOffset time, string text, string detail)
    {
        var title = text.ReplaceLineEndings(" ");
        var when = time.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        var item = new ListBoxItem
        {
            Tag = conversation,
            Content = new StackPanel
            {
                Spacing = 2,
                Margin = new Avalonia.Thickness(0, 6),
                Children =
                {
                    new TextBlock { Text = title, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 2, TextWrapping = TextWrapping.Wrap },
                    new TextBlock { Text = $"{when} · {detail}", Classes = { "secondary" } },
                },
            },
        };
        Avalonia.Automation.AutomationProperties.SetName(item, $"{title}, {when}, {detail}");
        return item;
    }

    public void ShowConversation(long conversation)
    {
        SearchBox.Text = string.Empty;
        Fill();
        if (List.Items.OfType<ListBoxItem>().FirstOrDefault(item => item.Tag is long id && id == conversation) is not { } item)
        {
            return;
        }

        List.SelectedItem = item;
        List.ScrollIntoView(item);
    }

    private void ShowSelected()
    {
        if (List.SelectedItem is not ListBoxItem { Tag: long conversation })
        {
            return;
        }

        Try(() =>
        {
            Entries.Children.Clear();
            foreach (var entry in _access.Conversation(conversation))
            {
                Entries.Children.Add(Render(entry));
            }
        });
    }

    private static StackPanel Render(HistoryEntry entry)
    {
        var heading = $"{entry.Time.ToLocalTime().ToString("t", CultureInfo.CurrentCulture)} · {Kind(entry)}";
        var panel = new StackPanel { Spacing = 4, Children = { new TextBlock { Text = heading, Classes = { "secondary" } } } };
        if (entry.Kind != HistoryEntryKind.Command)
        {
            panel.Children.Add(new SelectableTextBlock { Text = entry.Text, TextWrapping = TextWrapping.Wrap });
            return panel;
        }

        panel.Children.Add(new Border { Classes = { "code" }, Child = new SelectableTextBlock { Text = entry.Text, Classes = { "mono" }, TextWrapping = TextWrapping.Wrap } });
        if (entry.Detail is { Length: > 0 } output)
        {
            panel.Children.Add(new Border { Classes = { "code" }, Child = new SelectableTextBlock { Text = output, Classes = { "mono" }, TextWrapping = TextWrapping.Wrap } });
        }

        return panel;
    }

    private static string Kind(HistoryEntry entry) => entry.Kind switch
    {
        HistoryEntryKind.User => Strings.HistoryYou,
        HistoryEntryKind.Agent => Strings.HistoryAgent,
        HistoryEntryKind.Command => entry.ExitCode is { } code
            ? Strings.Format(Strings.HistoryCommandExit, entry.Terminal, code)
            : Strings.Format(Strings.HistoryCommand, entry.Terminal),
        _ => Strings.HistoryNotice,
    };

    // Keeping less deletes what's older at once, so it's confirmed first.
    private async Task ChangeRetentionAsync()
    {
        if (_isLoading || RetentionBox.SelectedIndex < 0)
        {
            return;
        }

        var days = s_retention[RetentionBox.SelectedIndex];
        var current = _access.RetentionDays;
        if (days == current)
        {
            return;
        }

        if (days is not null && (current is null || days < current))
        {
            var answer = await ChoiceWindow.AskAsync(
                this,
                Strings.HistoryShortenTitle,
                Strings.Format(Strings.HistoryShortenMessage, RetentionLabel(days)),
                details: null,
                hasDefault: false,
                Strings.Cancel,
                Strings.HistoryShortenConfirm).ConfigureAwait(true);
            if (answer != 1)
            {
                _isLoading = true;
                RetentionBox.SelectedIndex = Math.Max(0, Array.IndexOf(s_retention, current));
                _isLoading = false;
                return;
            }
        }

        Try(() => _access.SetRetentionDays(days));
        Fill();
    }

    private async Task ClearAsync()
    {
        IReadOnlyList<string> connections = [];
        Try(() => connections = _access.Connections());
        string[] choices = [Strings.Cancel, .. connections.Select(name => Strings.Format(Strings.HistoryClearConnection, name)), Strings.HistoryClearAll];
        var answer = await ChoiceWindow.AskAsync(this, Strings.HistoryClearTitle, Strings.HistoryClearMessage, details: null, hasDefault: false, choices).ConfigureAwait(true);
        if (answer is not { } index || index == 0)
        {
            return;
        }

        var connection = index <= connections.Count ? connections[index - 1] : null;
        Try(() => _access.Clear(connection));
        Fill();
        Status.Text = connection is null ? Strings.HistoryClearedAll : Strings.Format(Strings.HistoryClearedConnection, connection);
    }

    private void Try(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException exception)
        {
            Status.Text = Strings.Format(Strings.HistoryProblem, exception.Message);
        }
    }
}
