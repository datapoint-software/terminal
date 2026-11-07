using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Datapoint.Terminal.Sessions.Recording;
using Datapoint.Terminal.Shell;

namespace Datapoint.Terminal.Tests.Shell;

public sealed class RecordingsWindowTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("dpt-recordings-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private void Record(string name)
    {
        using var recorder = new SessionRecorder(File.Create(Path.Combine(_folder, name)), 60, 8, "web1 — shop", text => text);
        recorder.Observe("$ "u8);
        recorder.Mark("User: uptime");
        recorder.Observe("uptime\r\n 15:30 up 3 days\r\n$ "u8);
        recorder.Mark("Agent: df -h (check the disk)");
        recorder.Observe("df -h\r\n/dev/sda1  40G  12G  28G  30% /\r\n$ "u8);
    }

    [AvaloniaFact]
    public void Lists_recordings_newest_first_and_jumps_to_a_marker()
    {
        Record("20261106-090000-web1.cast");
        Record("20261107-153000-Local.cast");
        var window = new RecordingsWindow(_folder) { Width = 1000, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var items = window.List.ItemsSource!.Cast<RecordingsWindow.RecordingItem>().ToList();
        Assert.StartsWith("Local · ", items[0].Label, StringComparison.Ordinal);
        Assert.Equal(["User: uptime", "Agent: df -h (check the disk)"], window.Markers.ItemsSource!.Cast<RecordingsWindow.MarkerItem>().Select(marker => marker.Label), StringComparer.Ordinal);

        window.Markers.SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("/dev/sda1", window.Screen.Terminal!.Screen[3].GetText(), StringComparison.Ordinal);
        using var frame = window.CaptureRenderedFrame();
        Directory.CreateDirectory("rendered");
        frame?.Save("rendered/recordings.png", new PngBitmapEncoderOptions());
        window.Close();
    }

    [AvaloniaFact]
    public void Says_when_there_are_no_recordings()
    {
        var window = new RecordingsWindow(Path.Combine(_folder, "none"));
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.True(window.Empty.IsVisible);
        Assert.False(window.PlayButton.IsEnabled);
        window.Close();
    }
}
