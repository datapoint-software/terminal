using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Datapoint.Terminal.Agent.Files;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Shell;

// Open a File on a host: a path typed with completion from the host's folders, listed over the tab's own connection.
public sealed partial class OpenHostFileWindow : Window
{
    private const int MostEntries = 2000;

    private readonly IHostFiles? _files;
    private readonly Dictionary<string, IReadOnlyList<HostEntry>> _listed = new(StringComparer.Ordinal);
    private readonly DispatcherTimer _listTimer = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private bool _isClosed;

    public OpenHostFileWindow()
        : this(null, string.Empty, null, "~/")
    {
    }

    public OpenHostFileWindow(IHostFiles? files, string host, string? environment, string start)
    {
        _files = files;
        InitializeComponent();
        Title = Strings.Format(Strings.HostFileOpenTitle, host);
        Heading.Text = Title;
        Badge.Text = environment;
        Badge.IsVisible = environment is not null;
        Badge.Classes.Set("danger", Agent.EnvironmentLabels.IsProduction(environment));
        PathBox.Text = start;
        PathBox.TextChanged += (_, _) =>
        {
            _listTimer.Stop();
            _listTimer.Start();
        };
        PathBox.AddHandler(KeyDownEvent, OnPathKeyDown, RoutingStrategies.Tunnel);
        _listTimer.Tick += (_, _) => _ = ListAsync();
        EntryList.DoubleTapped += (_, _) => Choose();
        OpenButton.Click += (_, _) => Choose();
        CancelButton.Click += (_, _) => Close(null);
        Opened += (_, _) =>
        {
            PathBox.Focus();
            PathBox.CaretIndex = PathBox.Text?.Length ?? 0;
            _ = ListAsync();
        };
        Closed += (_, _) =>
        {
            _listTimer.Stop();
            _isClosed = true;
        };
    }

    public static Task<string?> AskAsync(Window owner, IHostFiles files, string host, string? environment, string start)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return new OpenHostFileWindow(files, host, environment, start).ShowDialog<string?>(owner);
    }

    private string Path => PathBox.Text ?? string.Empty;

    private async Task ListAsync()
    {
        _listTimer.Stop();
        var folder = HostPathCompletion.Split(Path).Folder;
        if (_files is null || ShellText.PathProblem(folder + "x") is not null)
        {
            Show([], null);
            return;
        }

        if (!_listed.TryGetValue(folder, out var entries))
        {
            try
            {
                entries = await _files.ListAsync(folder, MostEntries, CancellationToken.None).ConfigureAwait(true);
                _listed[folder] = entries;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException
                or Sessions.Ssh.Channels.SshChannelException or Sessions.Ssh.SshDisconnectedException)
            {
                Show([], Strings.Format(Strings.HostFileOpenFolderProblem, folder, exception.Message));
                return;
            }
        }

        // The path may have moved on while the folder was listed, or the sheet closed.
        if (_isClosed || !string.Equals(HostPathCompletion.Split(Path).Folder, folder, StringComparison.Ordinal))
        {
            return;
        }

        Show(HostPathCompletion.Matches(entries, HostPathCompletion.Split(Path).Prefix), null);
    }

    private void Show(IReadOnlyList<HostEntry> entries, string? problem)
    {
        EntryList.ItemsSource = entries.Select(entry => new HostEntryItem(entry)).ToList();
        EntryList.SelectedIndex = entries.Count > 0 ? 0 : -1;
        Problem.Text = problem;
        Problem.IsVisible = problem is not null;
    }

    private void OnPathKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down or Key.Up:
                Move(e.Key == Key.Down ? 1 : -1);
                break;
            case Key.Tab when e.KeyModifiers == KeyModifiers.None:
                Complete();
                break;
            case Key.Enter:
                Choose();
                break;
            case Key.Escape:
                Close(null);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void Move(int by)
    {
        var count = EntryList.ItemCount;
        if (count == 0)
        {
            return;
        }

        EntryList.SelectedIndex = Math.Clamp(EntryList.SelectedIndex + by, 0, count - 1);
        EntryList.ScrollIntoView(EntryList.SelectedIndex);
    }

    private void Complete()
    {
        if (EntryList.SelectedItem is not HostEntryItem item)
        {
            return;
        }

        SetPath(HostPathCompletion.Choose(HostPathCompletion.Split(Path).Folder, item.Entry));
    }

    // A folder is gone into; a file, or a path typed in full, is opened.
    private void Choose()
    {
        var (folder, prefix) = HostPathCompletion.Split(Path);
        if (EntryList.SelectedItem is HostEntryItem item && (prefix.Length == 0 || item.Entry.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            if (item.Entry.IsFolder)
            {
                SetPath(HostPathCompletion.Choose(folder, item.Entry));
                return;
            }

            Close(HostPathCompletion.Choose(folder, item.Entry));
            return;
        }

        if (ShellText.PathProblem(Path) is { } problem)
        {
            Problem.Text = problem;
            Problem.IsVisible = true;
            return;
        }

        Close(Path);
    }

    private void SetPath(string path)
    {
        PathBox.Text = path;
        PathBox.CaretIndex = path.Length;
        _ = ListAsync();
    }
}
