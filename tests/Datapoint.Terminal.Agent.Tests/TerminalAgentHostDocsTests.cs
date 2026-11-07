using Datapoint.Terminal.Agent.Hosts;
using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Agent.Tests.Fakes;
using Microsoft.Extensions.AI;
using static Datapoint.Terminal.Agent.Tests.Fakes.ScriptedChatClient;

namespace Datapoint.Terminal.Agent.Tests;

public sealed class TerminalAgentHostDocsTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("host-docs-").FullName;
    private readonly FakeUser _user = new();
    private readonly List<AgentEventArgs> _events = [];
    private readonly List<string> _ignored = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string Web1 => Path.Combine(_folder, "web1", "README.md");

    private Task WriteWeb1Async(string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Web1)!);
        return File.WriteAllTextAsync(Web1, text, Token);
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static Func<IReadOnlyList<ChatMessage>, ChatResponse> Tool(string name, params (string Key, string Value)[] values) =>
        _ => Call(name, values.ToDictionary(item => item.Key, item => (object?)item.Value, StringComparer.Ordinal));

    private async Task<(List<string?> Results, ScriptedChatClient Client)> RunAsync(bool canWrite, params Func<IReadOnlyList<ChatMessage>, ChatResponse>[] calls)
    {
        var results = new List<string?>();
        var steps = calls.Select((call, index) => index == 0 ? call : messages => { results.Add(LastToolResult(messages)); return call(messages); }).ToList();
        steps.Add(messages => { results.Add(LastToolResult(messages)); return Text("OK."); });
        var client = new ScriptedChatClient([.. steps]);
        using var agent = new TerminalAgent(client, _user, new SecretRedactor())
        {
            HostDocs = new HostDocsFolder(_folder, path => _ignored.Contains(path), canWrite),
            Clock = new FixedClock(new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero)),
        };
        agent.AddTerminal(new AgentTerminal("web1", new FakeTerminal(), ApprovalMode.AskForRisky));
        agent.EventRaised += (_, e) => _events.Add(e);
        await agent.SendAsync("Install nginx on web1", Token);
        return (results, client);
    }

    [Fact]
    public async Task Creates_the_documentation_and_says_so()
    {
        var (results, _) = await RunAsync(true, Tool("update_host_docs", ("connection", "WEB1"), ("section", "## System"), ("text", "Debian 12.")));

        Assert.Equal("Done.", results[0]);
        Assert.Equal("Debian 12.", HostDocument.Section(await File.ReadAllTextAsync(Web1, Token), "System"));
        var change = Assert.Single(_events, e => e.Kind == AgentEventKind.HostDocsChanged);
        Assert.Equal((Web1, "Updated \"System\" in the documentation for web1."), (change.Path, change.Text));
    }

    [Fact]
    public async Task Does_not_overwrite_a_file_it_has_not_read()
    {
        await WriteWeb1Async("# web1\n\n## System\n\nThe user's text.\n");

        var (results, _) = await RunAsync(true, Tool("update_host_docs", ("connection", "web1"), ("section", "System"), ("text", "Mine.")));

        Assert.StartsWith("Not changed: the file changed since you last read it", results[0], StringComparison.Ordinal);
        Assert.Contains("The user's text.", await File.ReadAllTextAsync(Web1, Token), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Updates_a_file_after_reading_it()
    {
        await WriteWeb1Async("# web1\n\n## System\n\nOld.\n");

        var (results, _) = await RunAsync(
            true,
            Tool("read_host_docs", ("connection", "web1")),
            Tool("update_host_docs", ("connection", "web1"), ("section", "System"), ("text", "New.")));

        Assert.Equal("# web1\n\n## System\n\nOld.\n\n## Maintenance log\n\nNo entries yet.\n", results[0]);
        Assert.Equal("Done.", results[1]);
    }

    [Fact]
    public async Task Logs_a_change_with_the_date()
    {
        var (results, _) = await RunAsync(true, Tool("log_host_change", ("connection", "web1"), ("entry", "Installed nginx 1.26.")));

        Assert.Equal("Done.", results[0]);
        var entry = Assert.Single(HostLog.Read(Path.Combine(_folder, "web1", "log")));
        Assert.Equal(("Installed nginx 1.26.", true, 2026), (entry.Text, entry.WithAgent, entry.When.Year));
    }

    [Fact]
    public async Task Refuses_text_that_looks_like_a_secret()
    {
        var (results, _) = await RunAsync(true, Tool("log_host_change", ("connection", "web1"), ("entry", "Set DB_PASSWORD=hunter22 in the unit file.")));

        Assert.StartsWith("Not changed: the text looks like it holds a secret", results[0], StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(_folder, "web1")));
    }

    [Fact]
    public async Task Leaves_the_log_to_log_host_change()
    {
        var (results, _) = await RunAsync(true, Tool("update_host_docs", ("connection", "web1"), ("section", "Maintenance log"), ("text", "Nothing.")));

        Assert.StartsWith("Not changed: add to the maintenance log with log_host_change", results[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Does_not_write_from_a_read_only_window()
    {
        var (results, _) = await RunAsync(false, Tool("log_host_change", ("connection", "web1"), ("entry", "Patched.")));

        Assert.StartsWith("Not changed: this window can't write", results[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Does_not_read_documentation_listed_in_terminalignore()
    {
        await WriteWeb1Async("# web1\n");
        _ignored.Add(Web1);

        var (results, _) = await RunAsync(true, Tool("read_host_docs", ("connection", "web1")));

        Assert.Equal("The documentation for \"web1\" is listed in .terminalignore, so you can't read or change it.", results[0]);
    }

    [Fact]
    public async Task Refuses_an_unknown_connection()
    {
        var (results, _) = await RunAsync(true, Tool("read_host_docs", ("connection", "../secrets")));

        Assert.StartsWith("There's no connection called", results[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Lists_the_documented_hosts()
    {
        await WriteWeb1Async("# web1\n");

        var (_, client) = await RunAsync(true, _ => Text("OK."));

        Assert.Contains("[Host documentation in hosts/ for: \"web1\"]", client.Requests[0][^1].Text, StringComparison.Ordinal);
    }
}
