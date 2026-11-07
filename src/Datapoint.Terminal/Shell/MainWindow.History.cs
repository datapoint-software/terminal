using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Agent.Projects.History;
using Datapoint.Terminal.Resources;
using Microsoft.Data.Sqlite;

namespace Datapoint.Terminal.Shell;

public sealed partial class MainWindow : IHistoryAccess
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
                Record(HistoryEntryKind.Command, e.Command ?? string.Empty, e.Terminal, e.Output, e.ExitCode, e.Connection);
                break;
            case AgentEventKind.Notice:
                Record(HistoryEntryKind.Notice, e.Text);
                break;
        }
    }

    // A read-only window leaves the history to the window that holds the project's lock. The store is opened on the first entry,
    // so a project that's never used with the agent gets no history file.
    private void Record(HistoryEntryKind kind, string text, string? terminal = null, string? detail = null, int? exitCode = null, string? connection = null)
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
                var history = OpenHistory();
                var now = DateTimeOffset.Now;
                _conversation ??= history.StartConversation(now);
                history.Add(new HistoryEntry(_conversation.Value, now, kind, text, terminal, detail, exitCode, connection));
            }
            catch (Exception exception) when (exception is SqliteException or HistoryException or IOException or UnauthorizedAccessException)
            {
                StopHistory(exception.Message);
            }
        }
    }

    // Read-only windows don't open the store, since opening it can upgrade its format. The file isn't created just to search it.
    private IReadOnlyList<HistoryEntry> SearchHistory(string words, int limit)
    {
        if (Project.IsReadOnly)
        {
            return [];
        }

        lock (_historyLock)
        {
            if (_isHistoryUnavailable || (_history is null && !File.Exists(Path.Combine(Project.PersonalPath, HistoryStore.FileName))))
            {
                return [];
            }

            try
            {
                return OpenHistory().Search(words, limit);
            }
            catch (Exception exception) when (exception is SqliteException or HistoryException)
            {
                throw new InvalidOperationException(exception.Message, exception);
            }
        }
    }

    // Opening applies the retention setting, so old entries go when a project is next used, not only while it stays open.
    private HistoryStore OpenHistory()
    {
        if (_history is not null)
        {
            return _history;
        }

        _history = HistoryStore.Open(Project.PersonalPath, _redactor);
        Prune(_history);
        return _history;
    }

    private void Prune(HistoryStore store)
    {
        if (_settings.HistoryDays is not { } days)
        {
            return;
        }

        store.DeleteOlderThan(DateTimeOffset.Now.AddDays(-days));
    }

    // For the history window: an existing history only, never created to be read, and nothing from a read-only window.
    private T WithHistory<T>(Func<HistoryStore, T> use, T empty)
    {
        if (Project.IsReadOnly)
        {
            return empty;
        }

        lock (_historyLock)
        {
            if (_isHistoryUnavailable || (_history is null && !File.Exists(Path.Combine(Project.PersonalPath, HistoryStore.FileName))))
            {
                return empty;
            }

            try
            {
                return use(OpenHistory());
            }
            catch (Exception exception) when (exception is SqliteException or HistoryException or IOException or UnauthorizedAccessException)
            {
                throw new InvalidOperationException(exception.Message, exception);
            }
        }
    }

    int? IHistoryAccess.RetentionDays => _settings.HistoryDays;

    IReadOnlyList<ConversationSummary> IHistoryAccess.Conversations() => WithHistory(store => store.Conversations(500), []);

    IReadOnlyList<HistoryEntry> IHistoryAccess.Conversation(long id) => WithHistory(store => store.Conversation(id), []);

    IReadOnlyList<HistoryEntry> IHistoryAccess.Search(string words) => WithHistory(store => store.Search(words, 200), []);

    IReadOnlyList<string> IHistoryAccess.Connections() => WithHistory(store => store.Connections(), []);

    // The current conversation ends with it, so what comes next starts a new one.
    void IHistoryAccess.Clear(string? connection) => WithHistory(
        store =>
        {
            if (connection is null)
            {
                store.DeleteAll();
                _conversation = null;
            }
            else
            {
                store.DeleteConnection(connection);
            }

            return 0;
        },
        0);

    // Applies to every project; this one is pruned now, others when they next open their history.
    void IHistoryAccess.SetRetentionDays(int? days)
    {
        _settings.SetHistoryDays(days);
        WithHistory(store => { Prune(store); return 0; }, 0);
        PruneRecordings(force: true);
    }

    private HistoryWindow? _historyWindow;

    private void ShowHistory() => ShowHistory(conversation: null);

    private void ShowHistory(long? conversation)
    {
        if (_historyWindow is null)
        {
            _historyWindow = new HistoryWindow(this) { Title = $"{Strings.HistoryTitle} — {Project.Name}" };
            _historyWindow.Closed += (_, _) => _historyWindow = null;
            _historyWindow.Show(this);
        }
        else
        {
            _historyWindow.Activate();
        }

        if (conversation is not { } id)
        {
            return;
        }

        _historyWindow.ShowConversation(id);
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
