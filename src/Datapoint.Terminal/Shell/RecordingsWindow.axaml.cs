using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Datapoint.Terminal.Controls;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Sessions.Recording;
using EmulatorTerminal = Datapoint.Terminal.Emulator.Terminal;

namespace Datapoint.Terminal.Shell;

// Plays a workspace's session recordings, with a list of who did what to jump to (product decision 45).
public sealed partial class RecordingsWindow : Window
{
    private static readonly double[] s_speeds = [0.5, 1, 2, 4, 8];

    private readonly string _folder;
    private readonly Action<TerminalView, EmulatorTerminal>? _style;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private SessionRecording? _recording;
    private RecordingPlayer? _player;
    private DateTimeOffset _lastTick;
    private bool _isUpdating;

    public RecordingsWindow() : this(string.Empty)
    {
    }

    public RecordingsWindow(string folder, Action<TerminalView, EmulatorTerminal>? style = null)
    {
        InitializeComponent();
        _folder = folder;
        _style = style;
        SpeedBox.ItemsSource = s_speeds.Select(speed => speed.ToString("0.#", CultureInfo.CurrentCulture) + "×").ToList();
        SpeedBox.SelectedIndex = 1;
        List.SelectionChanged += (_, _) => ShowSelected();
        Markers.SelectionChanged += (_, _) => JumpToMarker();
        PlayButton.Click += (_, _) => TogglePlay();
        PositionSlider.ValueChanged += (_, e) => OnPositionChanged(e.NewValue);
        DeleteButton.Click += (_, _) => _ = DeleteAsync();
        CloseButton.Click += (_, _) => Close();
        _timer.Tick += (_, _) => Advance();
        KeyDown += OnKeyDown;
        Closed += (_, _) => _timer.Stop();
        Fill();
    }

    public sealed record RecordingItem(string Path, string Label)
    {
        public override string ToString() => Label;
    }

    public sealed record MarkerItem(double Time, string Label)
    {
        public override string ToString() => FormatTime(Time) + "  " + Label;
    }

    private void Fill()
    {
        var files = Directory.Exists(_folder)
            ? new DirectoryInfo(_folder).EnumerateFiles("*.cast").OrderByDescending(file => file.Name, StringComparer.Ordinal).ToList()
            : [];
        List.ItemsSource = files.Select(file => new RecordingItem(file.FullName, Describe(file))).ToList();
        Empty.IsVisible = files.Count == 0;
        List.SelectedIndex = files.Count > 0 ? 0 : -1;
    }

    private void OnPositionChanged(double position)
    {
        if (_isUpdating)
        {
            return;
        }

        SeekTo(position);
    }

    // Files are named yyyyMMdd-HHmmss-tab.cast.
    private static string Describe(FileInfo file)
    {
        var name = System.IO.Path.GetFileNameWithoutExtension(file.Name);
        var tab = name.Length > 16 ? name[16..] : name;
        var when = DateTime.TryParseExact(name.Length >= 15 ? name[..15] : name, "yyyyMMdd-HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? time.ToString("g", CultureInfo.CurrentCulture)
            : file.LastWriteTime.ToString("g", CultureInfo.CurrentCulture);
        return $"{tab} · {when} · {Math.Max(1, file.Length / 1024).ToString(CultureInfo.CurrentCulture)} KB";
    }

    private void ShowSelected()
    {
        Pause();
        _recording = null;
        _player = null;
        DeleteButton.IsEnabled = List.SelectedItem is RecordingItem;
        if (List.SelectedItem is not RecordingItem item)
        {
            return;
        }

        try
        {
            // The file may still be written by an open tab.
            using var stream = new FileStream(item.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            _recording = SessionRecording.Parse(reader);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException or System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException)
        {
            Status.Text = Strings.Format(Strings.RecordingsUnreadable, exception.Message);
            Show(null);
            return;
        }

        Status.Text = Strings.RecordingsNote;
        _player = new RecordingPlayer(_recording);
        _player.TerminalReplaced += (_, _) => Show(_player.Terminal);
        Show(_player.Terminal);
        Markers.ItemsSource = _recording.Markers.Select(marker => new MarkerItem(marker.Time, marker.Data)).ToList();
        PositionSlider.Maximum = Math.Max(_recording.Duration, 0.01);
        Update();
    }

    private void Show(EmulatorTerminal? terminal)
    {
        PlayButton.IsEnabled = PositionSlider.IsEnabled = terminal is not null;
        Screen.Terminal = terminal;
        if (terminal is not null && _style is { } style)
        {
            style(Screen, terminal);
        }

        Screen.NotifyOutput();
    }

    private void TogglePlay()
    {
        if (_timer.IsEnabled)
        {
            Pause();
            return;
        }

        if (_player is not { } player)
        {
            return;
        }

        if (player.IsAtEnd)
        {
            SeekTo(0);
        }

        _lastTick = DateTimeOffset.UtcNow;
        _timer.Start();
        PlayButton.Content = Strings.RecordingsPause;
    }

    private void Pause()
    {
        _timer.Stop();
        PlayButton.Content = Strings.RecordingsPlay;
    }

    private void Advance()
    {
        if (_player is not { } player || _recording is null)
        {
            Pause();
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var speed = s_speeds[Math.Max(0, SpeedBox.SelectedIndex)];
        player.Seek(player.Position + ((now - _lastTick).TotalSeconds * speed));
        _lastTick = now;
        Screen.NotifyOutput();
        Update();
        if (!player.IsAtEnd || player.Position < _recording.Duration)
        {
            return;
        }

        Pause();
    }

    private void SeekTo(double position)
    {
        if (_player is not { } player)
        {
            return;
        }

        player.Seek(position);
        Screen.NotifyOutput();
        Update();
    }

    // Just past the marker, so the output that came with it is on screen.
    private void JumpToMarker()
    {
        if (Markers.SelectedItem is not MarkerItem marker)
        {
            return;
        }

        Pause();
        SeekTo(marker.Time + 0.001);
    }

    private void Update()
    {
        if (_player is null || _recording is null)
        {
            return;
        }

        _isUpdating = true;
        PositionSlider.Value = _player.Position;
        _isUpdating = false;
        TimeText.Text = FormatTime(_player.Position) + " / " + FormatTime(_recording.Duration);
    }

    private static string FormatTime(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return time.TotalHours >= 1
            ? time.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : time.ToString(@"m\:ss", CultureInfo.InvariantCulture);
    }

    private async Task DeleteAsync()
    {
        if (List.SelectedItem is not RecordingItem item)
        {
            return;
        }

        var choice = await ChoiceWindow.AskAsync(this, Strings.RecordingsDeleteTitle, Strings.Format(Strings.RecordingsDeleteMessage, item.Label), Strings.Cancel, Strings.RecordingsDeleteConfirm).ConfigureAwait(true);
        if (choice != 1)
        {
            return;
        }

        try
        {
            File.Delete(item.Path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Status.Text = exception.Message;
            return;
        }

        Fill();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
            return;
        }

        if (e.Key != Key.Space || e.Source is TextBox or ComboBox or ListBoxItem)
        {
            return;
        }

        e.Handled = true;
        TogglePlay();
    }
}
