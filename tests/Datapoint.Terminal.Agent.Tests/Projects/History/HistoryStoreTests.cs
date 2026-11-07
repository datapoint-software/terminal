using Datapoint.Terminal.Agent.Projects.History;
using Datapoint.Terminal.Agent.Safety;

namespace Datapoint.Terminal.Agent.Tests.Projects.History;

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
        Assert.Equal(new HistoryEntry(conversation, s_start.AddSeconds(1), HistoryEntryKind.Command, "df -h", "web1", "/dev/sda1 50G 20G", 0), entries[1]);
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
}
