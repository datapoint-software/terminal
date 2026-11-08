using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Agent.Workspaces.History;

namespace Datapoint.Terminal.Agent.Tests.Workspaces.History;

public sealed class HistoryStoreTests : IDisposable
{
    private static readonly DateTimeOffset s_start = new(2026, 11, 6, 9, 0, 0, TimeSpan.Zero);

    private readonly string _folder = Directory.CreateTempSubdirectory("datapoint-history-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string DatabasePath => Path.Combine(_folder, HistoryStore.FileName);

    private HistoryStore Open() => HistoryStore.Open(_folder, new SecretRedactor());

    [Fact]
    public void Keeps_a_conversation_in_order_across_reopening()
    {
        long conversation;
        using (var store = Open())
        {
            conversation = store.StartConversation(s_start);
            store.Add(new HistoryEntry(conversation, s_start, HistoryEntryKind.User, "How full is the disk?"));
            store.Add(new HistoryEntry(conversation, s_start.AddSeconds(1), HistoryEntryKind.Command, "df -h", "web1", "/dev/sda1 50G 20G", 0));
            store.Add(new HistoryEntry(conversation, s_start.AddSeconds(2), HistoryEntryKind.Agent, "It's 42% full."));
        }

        using var reopened = Open();
        var entries = reopened.Conversation(conversation);

        Assert.Equal([HistoryEntryKind.User, HistoryEntryKind.Command, HistoryEntryKind.Agent], entries.Select(entry => entry.Kind));
        Assert.Equal(new HistoryEntry(conversation, s_start.AddSeconds(1), HistoryEntryKind.Command, "df -h", "web1", "/dev/sda1 50G 20G", 0) { Id = 2 }, entries[1]);
    }

    [Fact]
    public void Redacts_before_storing()
    {
        using (var store = Open())
        {
            var conversation = store.StartConversation(s_start);
            store.Add(new HistoryEntry(conversation, s_start, HistoryEntryKind.Command, "export DB_PASSWORD=hunter2hunter2", Detail: "token=abcdefghijkl"));
        }

        var bytes = File.ReadAllBytes(DatabasePath);
        Assert.Equal(-1, bytes.AsSpan().IndexOf("hunter2"u8));
        Assert.Equal(-1, bytes.AsSpan().IndexOf("abcdefghijkl"u8));
    }

    [Fact]
    public void Uses_the_rollback_journal_and_a_private_file()
    {
        using (var store = Open())
        {
            store.StartConversation(s_start);
        }

        Assert.False(File.Exists(DatabasePath + "-wal"));
        var header = File.ReadAllBytes(DatabasePath)[18..20];
        Assert.Equal(new byte[] { 1, 1 }, header);
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(DatabasePath));
    }

    [Fact]
    public void Searches_words_newest_first_without_query_syntax_errors()
    {
        using var store = Open();
        var conversation = store.StartConversation(s_start);
        store.Add(new HistoryEntry(conversation, s_start, HistoryEntryKind.Command, "systemctl restart nginx", Detail: "ok"));
        store.Add(new HistoryEntry(conversation, s_start.AddMinutes(1), HistoryEntryKind.Agent, "Restarted nginx on web1."));
        store.Add(new HistoryEntry(conversation, s_start.AddMinutes(2), HistoryEntryKind.Agent, "Nothing to do."));

        Assert.Equal(["Restarted nginx on web1.", "systemctl restart nginx"], store.Search("nginx", 10).Select(entry => entry.Text), StringComparer.Ordinal);
        Assert.Equal(["Restarted nginx on web1."], store.Search("nginx web1", 10).Select(entry => entry.Text), StringComparer.Ordinal);
        Assert.Empty(store.Search("\"nginx\" OR (", 10));
        Assert.Empty(store.Search("   ", 10));

        var found = store.Search("systemctl", 10).Single();
        Assert.Equal(found.Id, store.Search("restart", 10)[^1].Id);
        Assert.Equal(found.Id + 3, store.Add(new HistoryEntry(conversation, s_start, HistoryEntryKind.Agent, "Later.")));
        Assert.Equal("ok", store.Find(found.Id)!.Detail);
        Assert.Null(store.Find(found.Id + 100));
    }

    [Fact]
    public void Deletes_entries_older_than_the_cutoff()
    {
        using var store = Open();
        var old = store.StartConversation(s_start);
        store.Add(new HistoryEntry(old, s_start, HistoryEntryKind.User, "old question"));
        var recent = store.StartConversation(s_start.AddDays(10));
        store.Add(new HistoryEntry(recent, s_start.AddDays(10), HistoryEntryKind.User, "new question"));

        Assert.Equal(1, store.DeleteOlderThan(s_start.AddDays(5)));

        Assert.Empty(store.Conversation(old));
        Assert.Empty(store.Search("old", 10));
        Assert.Single(store.Conversation(recent));
    }

    [Fact]
    public void Keeps_the_start_and_end_of_long_output()
    {
        using var store = Open();
        var conversation = store.StartConversation(s_start);
        var output = "start" + new string('x', HistoryStore.MaxDetailCharacters) + "end";
        store.Add(new HistoryEntry(conversation, s_start, HistoryEntryKind.Command, "yes", Detail: output));

        var detail = store.Conversation(conversation)[0].Detail!;

        Assert.StartsWith("start", detail, StringComparison.Ordinal);
        Assert.EndsWith("end", detail, StringComparison.Ordinal);
        Assert.Contains("characters left out", detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_a_history_from_a_newer_version()
    {
        using (var store = Open())
        {
            store.StartConversation(s_start);
        }

        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={DatabasePath};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version = 99";
            command.ExecuteNonQuery();
        }

        Assert.Throws<HistoryException>(Open);
    }

    [Fact]
    public void Lists_conversations_newest_first_with_the_first_message()
    {
        using var store = Open();
        var first = store.StartConversation(s_start);
        store.Add(new HistoryEntry(first, s_start, HistoryEntryKind.Notice, "Connected."));
        store.Add(new HistoryEntry(first, s_start, HistoryEntryKind.User, "How full is the disk?"));
        var second = store.StartConversation(s_start.AddHours(1));
        store.Add(new HistoryEntry(second, s_start.AddHours(1), HistoryEntryKind.Agent, "Hello."));
        store.StartConversation(s_start.AddHours(2));

        Assert.Equal(
            [new ConversationSummary(second, s_start.AddHours(1), "Hello.", 1), new ConversationSummary(first, s_start, "How full is the disk?", 2)],
            store.Conversations(10));
    }

    [Fact]
    public void Clears_one_connection_and_keeps_the_rest()
    {
        using var store = Open();
        var conversation = store.StartConversation(s_start);
        store.Add(new HistoryEntry(conversation, s_start, HistoryEntryKind.User, "Check both"));
        store.Add(new HistoryEntry(conversation, s_start, HistoryEntryKind.Command, "uptime", "web1", "up quokka days", 0, "web1"));
        store.Add(new HistoryEntry(conversation, s_start, HistoryEntryKind.Command, "uptime", "db 2", "up 9 days", 0, "db"));

        Assert.Equal(["db", "web1"], store.Connections(), StringComparer.Ordinal);
        Assert.Equal(1, store.DeleteConnection("web1"));

        Assert.Equal(["Check both", "uptime"], store.Conversation(conversation).Select(entry => entry.Text), StringComparer.Ordinal);
        Assert.Empty(store.Search("quokka", 10));
        Assert.Equal(["db"], store.Connections(), StringComparer.Ordinal);
        Assert.Equal(-1, File.ReadAllBytes(DatabasePath).AsSpan().IndexOf("quokka"u8));
    }

    [Fact]
    public void Clears_everything_from_the_file()
    {
        using (var store = Open())
        {
            var conversation = store.StartConversation(s_start);
            store.Add(new HistoryEntry(conversation, s_start, HistoryEntryKind.User, "remember the word zanzibar"));
            store.DeleteAll();
            Assert.Empty(store.Conversations(10));
        }

        Assert.Equal(-1, File.ReadAllBytes(DatabasePath).AsSpan().IndexOf("zanzibar"u8));
    }

    [Fact]
    public void Upgrades_a_first_format_history()
    {
        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={DatabasePath};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE conversations (id INTEGER PRIMARY KEY, started INTEGER NOT NULL);
                CREATE TABLE entries (id INTEGER PRIMARY KEY, conversation INTEGER NOT NULL REFERENCES conversations (id) ON DELETE CASCADE,
                    time INTEGER NOT NULL, kind TEXT NOT NULL, terminal TEXT, text TEXT NOT NULL, detail TEXT, exit_code INTEGER);
                INSERT INTO conversations VALUES (1, 0);
                INSERT INTO entries (conversation, time, kind, text) VALUES (1, 0, 'User', 'old question');
                PRAGMA user_version = 1;
                """;
            command.ExecuteNonQuery();
        }

        using var store = Open();
        store.Add(new HistoryEntry(1, s_start, HistoryEntryKind.Command, "ls", "web1", "a b", 0, "web1"));

        Assert.Equal([null, "web1"], store.Conversation(1).Select(entry => entry.Connection), StringComparer.Ordinal);
    }

    [Fact]
    public void Keeps_the_latest_scrollback_of_each_connection_redacted()
    {
        using var store = Open();
        for (var index = 1; index <= 5; index++)
        {
            store.SaveScrollback("web1", s_start.AddMinutes(index), $"session {index}\nexport TOKEN=ghp_abcdefghijklmnopqrstuvwxyz0123456789");
        }

        var latest = store.LatestScrollback("web1");

        Assert.Equal(s_start.AddMinutes(5), latest!.Time);
        Assert.StartsWith("session 5\n", latest.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("ghp_abcdefghijklmnopqrstuvwxyz0123456789", latest.Text, StringComparison.Ordinal);
        Assert.Null(store.LatestScrollback("db1"));
    }

    [Fact]
    public void Keeps_the_end_of_a_long_scrollback()
    {
        using var store = Open();
        var text = string.Join("\n", Enumerable.Range(1, 40_000).Select(index => $"line {index}"));

        store.SaveScrollback("web1", s_start, text);

        var kept = store.LatestScrollback("web1")!.Text;
        Assert.InRange(kept.Length, 1, HistoryStore.MaxScrollbackCharacters);
        Assert.EndsWith("line 40000", kept, StringComparison.Ordinal);
        Assert.StartsWith("line ", kept, StringComparison.Ordinal);
    }

    [Fact]
    public void Clears_scrollback_with_its_connection_and_with_age()
    {
        using var store = Open();
        store.SaveScrollback("web1", s_start, "old");
        store.SaveScrollback("db1", s_start.AddDays(10), "new");

        Assert.Equal(["db1", "web1"], store.Connections());
        store.DeleteConnection("db1");
        Assert.Null(store.LatestScrollback("db1"));

        store.DeleteOlderThan(s_start.AddDays(1));
        Assert.Null(store.LatestScrollback("web1"));
    }

    [Fact]
    public void Recalls_the_agents_last_reply_where_it_last_worked_on_a_connection()
    {
        using var store = Open();
        var conversation = store.StartConversation(s_start);
        store.Add(new HistoryEntry(conversation, s_start, HistoryEntryKind.Command, "systemctl reload nginx", "web1", string.Empty, 0, "web1"));
        store.Add(new HistoryEntry(conversation, s_start.AddSeconds(5), HistoryEntryKind.Agent, "Reloaded nginx."));

        var work = store.LastWorkOn("web1");

        Assert.Equal((conversation, s_start, "Reloaded nginx."), work);
        Assert.Null(store.LastWorkOn("db1"));
    }
}
