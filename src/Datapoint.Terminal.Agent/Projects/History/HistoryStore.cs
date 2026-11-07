using System.Globalization;
using Datapoint.Terminal.Agent.Safety;
using Microsoft.Data.Sqlite;

namespace Datapoint.Terminal.Agent.Projects.History;

// A project's conversations and the agent's commands, in .terminal/history.db (product decisions 52 and 90). Everything is
// redacted before it's stored. The rollback journal is used rather than WAL, whose extra files break when a sync service copies
// the folder; one window writes to a project at a time, so WAL's concurrency isn't needed.
public sealed class HistoryStore : IDisposable
{
    public const string FileName = "history.db";

    // Long outputs keep their start and end, as the agent sees them.
    public const int MaxDetailCharacters = 64 * 1024;

    // 2 added the connection each entry came from.
    private const int Version = 2;

    private readonly SqliteConnection _connection;
    private readonly SecretRedactor _redactor;
    private readonly Lock _lock = new();

    private HistoryStore(SqliteConnection connection, SecretRedactor redactor)
    {
        _connection = connection;
        _redactor = redactor;
    }

    public static HistoryStore Open(string personalFolder, SecretRedactor redactor)
    {
        ArgumentNullException.ThrowIfNull(redactor);
        var path = Path.Combine(personalFolder, FileName);
        CreatePrivateFile(path);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString());
        try
        {
            connection.Open();
            Prepare(connection);
            return new HistoryStore(connection, redactor);
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    public long StartConversation(DateTimeOffset time)
    {
        lock (_lock)
        {
            using var command = Command("INSERT INTO conversations (started) VALUES ($time) RETURNING id", ("$time", time.ToUnixTimeMilliseconds()));
            return (long)command.ExecuteScalar()!;
        }
    }

    public void Add(HistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_lock)
        {
            using var command = Command(
                "INSERT INTO entries (conversation, time, kind, terminal, text, detail, exit_code, connection) VALUES ($conversation, $time, $kind, $terminal, $text, $detail, $exit, $connection)",
                ("$conversation", entry.Conversation),
                ("$time", entry.Time.ToUnixTimeMilliseconds()),
                ("$kind", entry.Kind.ToString()),
                ("$terminal", entry.Terminal),
                ("$text", _redactor.Redact(entry.Text)),
                ("$detail", entry.Detail is null ? null : Shorten(_redactor.Redact(entry.Detail))),
                ("$exit", entry.ExitCode),
                ("$connection", entry.Connection));
            command.ExecuteNonQuery();
        }
    }

    public IReadOnlyList<HistoryEntry> Conversation(long conversation)
    {
        lock (_lock)
        {
            using var command = Command($"SELECT {Columns} FROM entries WHERE conversation = $conversation ORDER BY id", ("$conversation", conversation));
            return Read(command);
        }
    }

    // Every word must appear, as typed: the words are quoted, so FTS5's own syntax can't make a query fail.
    public IReadOnlyList<HistoryEntry> Search(string text, int limit)
    {
        ArgumentNullException.ThrowIfNull(text);
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return [];
        }

        var query = string.Join(' ', words.Select(word => "\"" + word.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""));
        lock (_lock)
        {
            using var command = Command(
                $"SELECT {Columns} FROM entry_search JOIN entries ON entries.id = entry_search.rowid WHERE entry_search MATCH $query ORDER BY entries.time DESC, entries.id DESC LIMIT $limit",
                ("$query", query),
                ("$limit", limit));
            return Read(command);
        }
    }

    // Newest first.
    public IReadOnlyList<ConversationSummary> Conversations(int limit)
    {
        lock (_lock)
        {
            using var command = Command(
                """
                SELECT c.id, c.started, count(e.id),
                    coalesce((SELECT text FROM entries WHERE conversation = c.id AND kind = 'User' ORDER BY id LIMIT 1),
                             (SELECT text FROM entries WHERE conversation = c.id ORDER BY id LIMIT 1), '')
                FROM conversations c JOIN entries e ON e.conversation = c.id
                GROUP BY c.id ORDER BY c.started DESC, c.id DESC LIMIT $limit
                """,
                ("$limit", limit));
            var summaries = new List<ConversationSummary>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                summaries.Add(new ConversationSummary(reader.GetInt64(0), DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(1)), reader.GetString(3), reader.GetInt32(2)));
            }

            return summaries;
        }
    }

    public IReadOnlyList<string> Connections()
    {
        lock (_lock)
        {
            using var command = Command("SELECT DISTINCT connection FROM entries WHERE connection IS NOT NULL ORDER BY connection COLLATE NOCASE");
            var names = new List<string>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                names.Add(reader.GetString(0));
            }

            return names;
        }
    }

    // Clearing per connection (product decision 52) removes what was recorded from its terminals; the rest of each conversation stays.
    public int DeleteConnection(string connection) =>
        Delete("DELETE FROM entries WHERE connection = $connection", ("$connection", connection));

    // Deleted pages are overwritten (secure_delete), and the file is vacuumed to give the space back.
    public void DeleteAll()
    {
        Delete("DELETE FROM entries");
        lock (_lock)
        {
            using var vacuum = Command("VACUUM");
            vacuum.ExecuteNonQuery();
        }
    }

    // For the retention setting: entries older than the cutoff go, and so do conversations left empty.
    public int DeleteOlderThan(DateTimeOffset cutoff) =>
        Delete("DELETE FROM entries WHERE time < $cutoff", ("$cutoff", cutoff.ToUnixTimeMilliseconds()));

    // Conversations left empty go too.
    private int Delete(string sql, params (string Name, object? Value)[] parameters)
    {
        lock (_lock)
        {
            using var transaction = _connection.BeginTransaction();
            using var entries = Command(sql, parameters);
            entries.Transaction = transaction;
            var deleted = entries.ExecuteNonQuery();
            using var conversations = Command("DELETE FROM conversations WHERE id NOT IN (SELECT conversation FROM entries)");
            conversations.Transaction = transaction;
            conversations.ExecuteNonQuery();

            // The search index only marks rows deleted until its segments are merged; merging drops their words for good.
            using var merge = Command("INSERT INTO entry_search (entry_search) VALUES ('optimize')");
            merge.Transaction = transaction;
            merge.ExecuteNonQuery();
            transaction.Commit();
            return deleted;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _connection.Dispose();
        }
    }

    private const string Columns = "entries.conversation, entries.time, entries.kind, entries.terminal, entries.text, entries.detail, entries.exit_code, entries.connection";

    private SqliteCommand Command(string sql, params (string Name, object? Value)[] parameters)
    {
        var command = _connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        return command;
    }

    private static List<HistoryEntry> Read(SqliteCommand command)
    {
        var entries = new List<HistoryEntry>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            entries.Add(new HistoryEntry(
                reader.GetInt64(0),
                DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(1)),
                Enum.TryParse<HistoryEntryKind>(reader.GetString(2), out var kind) ? kind : HistoryEntryKind.Notice,
                reader.GetString(4),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetInt32(6),
                reader.IsDBNull(7) ? null : reader.GetString(7)));
        }

        return entries;
    }

    private static void Prepare(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version";
        var version = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        if (version > Version)
        {
            throw new HistoryException($"The history was written by a newer version of Datapoint Terminal (format {version}), so it isn't changed.");
        }

        var migrate = version == 1 ? "ALTER TABLE entries ADD COLUMN connection TEXT;" : string.Empty;
        command.CommandText = $"""
            PRAGMA journal_mode = DELETE;
            PRAGMA foreign_keys = ON;
            PRAGMA secure_delete = ON;
            CREATE TABLE IF NOT EXISTS conversations (
                id INTEGER PRIMARY KEY,
                started INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS entries (
                id INTEGER PRIMARY KEY,
                conversation INTEGER NOT NULL REFERENCES conversations (id) ON DELETE CASCADE,
                time INTEGER NOT NULL,
                kind TEXT NOT NULL,
                terminal TEXT,
                text TEXT NOT NULL,
                detail TEXT,
                exit_code INTEGER,
                connection TEXT);
            {migrate}
            CREATE INDEX IF NOT EXISTS entries_by_conversation ON entries (conversation);
            CREATE INDEX IF NOT EXISTS entries_by_time ON entries (time);
            CREATE VIRTUAL TABLE IF NOT EXISTS entry_search USING fts5 (text, detail, content = 'entries', content_rowid = 'id');
            CREATE TRIGGER IF NOT EXISTS entries_added AFTER INSERT ON entries BEGIN
                INSERT INTO entry_search (rowid, text, detail) VALUES (new.id, new.text, coalesce(new.detail, ''));
            END;
            CREATE TRIGGER IF NOT EXISTS entries_removed AFTER DELETE ON entries BEGIN
                INSERT INTO entry_search (entry_search, rowid, text, detail) VALUES ('delete', old.id, old.text, coalesce(old.detail, ''));
            END;
            PRAGMA user_version = {Version};
            """;
        command.ExecuteNonQuery();
    }

    // SQLite gives its journal the database file's permissions, so creating the file first keeps both readable only by the user.
    private static void CreatePrivateFile(string path)
    {
        if (File.Exists(path))
        {
            return;
        }

        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        try
        {
            using var stream = new FileStream(path, options);
        }
        catch (IOException) when (File.Exists(path))
        {
        }
    }

    private static string Shorten(string text)
    {
        if (text.Length <= MaxDetailCharacters)
        {
            return text;
        }

        var half = MaxDetailCharacters / 2;
        return text[..half] + $"\n[... {text.Length - MaxDetailCharacters} characters left out ...]\n" + text[^half..];
    }
}
