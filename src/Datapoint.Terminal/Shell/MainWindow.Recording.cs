using System.Collections.Concurrent;
using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Emulator.ShellIntegration;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Sessions;
using Datapoint.Terminal.Sessions.Recording;

namespace Datapoint.Terminal.Shell;

// Product decisions 45, 58 and 75: each tab is recorded as it happens, with markers for who typed each command and what the
// agent did beside the terminal. Recordings stay in the project's personal folder, redacted like its history, and are pruned
// with it. They're the user's own record, not an audit log.
public sealed partial class MainWindow
{
    public const string RecordingsFolderName = "recordings";

    // Looked up from the agent's thread by terminal name, so not through the tab list.
    private readonly ConcurrentDictionary<string, SessionRecorder> _recorders = new(StringComparer.OrdinalIgnoreCase);
    private bool _hasPrunedRecordings;

    private string RecordingsFolder => Path.Combine(Project.PersonalPath, RecordingsFolderName);

    private void StartRecording(TerminalTab tab, TerminalConnection connection)
    {
        if (!_settings.RecordSessions || Project.IsReadOnly)
        {
            return;
        }

        try
        {
            PruneRecordings();
            var stream = CreateRecordingFile(tab.Name);
            var recorder = new SessionRecorder(stream, connection.Terminal.Columns, connection.Terminal.Rows, $"{tab.Label} — {Project.Name}", _redactor.Redact);
            tab.Recorder = recorder;
            _recorders[tab.Name] = recorder;
            connection.OutputObserver = recorder;
            connection.OutputArrived += (_, _) => MarkUserCommand(tab, connection);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _chat.AddNotice(Strings.Format(Strings.NoticeRecordingFailed, exception.Message));
        }
    }

    private FileStream CreateRecordingFile(string tabName)
    {
        var folder = RecordingsFolder;
        var name = $"{DateTimeOffset.Now:yyyyMMdd-HHmmss}-{Agent.Hosts.HostDocument.FileNameFor(tabName)[..^3]}.cast";
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.Read };
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(folder);
        }
        else
        {
            Directory.CreateDirectory(folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        return new FileStream(Path.Combine(folder, name), options);
    }

    // Runs on the connection's reading thread, right after the emulator has seen the output.
    private static void MarkUserCommand(TerminalTab tab, TerminalConnection connection)
    {
        var current = connection.Terminal.CurrentCommand;
        if (tab.Recorder is not { } recorder || current is not { OutputLine: not null } || ReferenceEquals(current, tab.LastMarkedCommand))
        {
            return;
        }

        tab.LastMarkedCommand = current;
        if (current.Source == CommandSource.Agent)
        {
            return;
        }

        recorder.Mark(Strings.Format(Strings.RecordingUserCommand, current.CommandLine ?? string.Empty));
    }

    private void OnRecordingEvent(object? sender, AgentEventArgs e)
    {
        if (e.Terminal is null || !_recorders.TryGetValue(e.Terminal, out var recorder))
        {
            return;
        }

        switch (e.Kind)
        {
            case AgentEventKind.CommandStarted:
                recorder.Mark(Strings.Format(Strings.RecordingAgentCommand, e.Command ?? string.Empty, e.Text));
                break;
            case AgentEventKind.Notice:
                recorder.Mark(Strings.Format(Strings.RecordingAgentAction, e.Text));
                break;
        }
    }

    private void StopRecording(TerminalTab tab)
    {
        _recorders.TryRemove(new KeyValuePair<string, SessionRecorder>(tab.Name, tab.Recorder!));
        tab.Recorder?.Dispose();
        tab.Recorder = null;
    }

    // Once per window, by the history's retention setting; a recording still open is never old enough to go.
    private void PruneRecordings(bool force = false)
    {
        if ((_hasPrunedRecordings && !force) || _settings.HistoryDays is not { } days || !Directory.Exists(RecordingsFolder))
        {
            _hasPrunedRecordings = true;
            return;
        }

        _hasPrunedRecordings = true;
        var cutoff = DateTime.UtcNow.AddDays(-days);
        foreach (var file in new DirectoryInfo(RecordingsFolder).EnumerateFiles("*.cast").Where(file => file.LastWriteTimeUtc < cutoff))
        {
            try
            {
                file.Delete();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Another window may still be writing it; it goes next time.
            }
        }
    }

    private RecordingsWindow? _recordingsWindow;

    private void ShowRecordings()
    {
        if (_recordingsWindow is { } open)
        {
            open.Activate();
            return;
        }

        _recordingsWindow = new RecordingsWindow(RecordingsFolder, ApplyAppearance) { Title = $"{Strings.RecordingsTitle} — {Project.Name}" };
        _recordingsWindow.Closed += (_, _) => _recordingsWindow = null;
        _recordingsWindow.Show(this);
    }
}
