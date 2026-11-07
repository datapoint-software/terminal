using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Agent.Projects.History;
using Datapoint.Terminal.Resources;
using Microsoft.Data.Sqlite;

namespace Datapoint.Terminal.Shell;

public sealed partial class MainWindow
{
    private readonly Lock _historyLock = new();
    private HistoryStore? _history;
    private bool _isHistoryUnavailable;
    private long? _conversation;

    // Messages come from the interface thread and the agent's events from its own, so the store is used under a lock.
    private void OnHistoryEvent(object? sender, AgentEventArgs e)
    {
        switch (e.Kind)
        {
            case AgentEventKind.Reply:
                Record(HistoryEntryKind.Agent, e.Text);
                break;
            case AgentEventKind.CommandFinished:
                Record(HistoryEntryKind.Command, e.Command ?? string.Empty, e.Terminal, e.Output, e.ExitCode);
                break;
            case AgentEventKind.Notice:
                Record(HistoryEntryKind.Notice, e.Text);
                break;
        }
    }

    // A read-only window leaves the history to the window that holds the project's lock. The store is opened on the first entry,
    // so a project that's never used with the agent gets no history file.
    private void Record(HistoryEntryKind kind, string text, string? terminal = null, string? detail = null, int? exitCode = null)
    {
        if (Project.IsReadOnly)
        {
            return;
        }

        lock (_historyLock)
        {
            if (_isHistoryUnavailable)
            {
                return;
            }

            try
            {
                _history ??= HistoryStore.Open(Project.PersonalPath, _redactor);
                var now = DateTimeOffset.Now;
                _conversation ??= _history.StartConversation(now);
                _history.Add(new HistoryEntry(_conversation.Value, now, kind, text, terminal, detail, exitCode));
            }
            catch (Exception exception) when (exception is SqliteException or HistoryException or IOException or UnauthorizedAccessException)
            {
                StopHistory(exception.Message);
            }
        }
    }

    private void EndConversation()
    {
        lock (_historyLock)
        {
            _conversation = null;
        }
    }

    private void StopHistory(string problem)
    {
        _isHistoryUnavailable = true;
        _history?.Dispose();
        _history = null;
        Avalonia.Threading.Dispatcher.UIThread.Post(() => _chat.AddNotice(Strings.Format(Strings.NoticeHistoryUnavailable, problem)));
    }

    private void CloseHistory()
    {
        lock (_historyLock)
        {
            _isHistoryUnavailable = true;
            _history?.Dispose();
            _history = null;
        }
    }
}
