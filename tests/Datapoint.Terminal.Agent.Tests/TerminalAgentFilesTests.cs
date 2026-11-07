using System.Text.Json;
using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Agent.Tests.Fakes;
using Microsoft.Extensions.AI;
using static Datapoint.Terminal.Agent.Tests.Fakes.ScriptedChatClient;

namespace Datapoint.Terminal.Agent.Tests;

public sealed class TerminalAgentFilesTests
{
    private const string Config = "server {\n    listen 80;\n    root /var/www;\n}\n";

    private readonly FakeUser _user = new();
    private readonly FakeTerminal _terminal = new();
    private readonly FakeHostFiles _files = new();
    private readonly List<string?> _results = [];
    private readonly List<AgentEventArgs> _events = [];

    public TerminalAgentFilesTests()
    {
        _files.Files["/etc/nginx/nginx.conf"] = Config;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Func<IReadOnlyList<ChatMessage>, ChatResponse> Tool(string name, string json) =>
        _ => Call(name, JsonDocument.Parse(json).RootElement.EnumerateObject().ToDictionary(property => property.Name, property => (object?)property.Value.Clone(), StringComparer.Ordinal));

    private static Func<IReadOnlyList<ChatMessage>, ChatResponse> Edit(string old = "listen 80;", string replacement = "listen 443 ssl;", string path = "/etc/nginx/nginx.conf") =>
        Tool("edit_file", JsonSerializer.Serialize(new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["terminal"] = "web1",
            ["path"] = path,
            ["purpose"] = "serve TLS",
            ["edits"] = new[] { new Dictionary<string, string>(StringComparer.Ordinal) { ["old_text"] = old, ["new_text"] = replacement } },
        }));

    private async Task RunAsync(ApprovalMode mode, params Func<IReadOnlyList<ChatMessage>, ChatResponse>[] steps)
    {
        var script = steps.Select((step, index) => index == 0 ? step : Record(step)).Append(Record(_ => Text("Done."))).ToArray();
        using var agent = new TerminalAgent(new ScriptedChatClient(script), _user, new SecretRedactor()) { Clock = new FixedClock(new DateTimeOffset(2026, 11, 7, 15, 30, 12, TimeSpan.Zero)) };
        agent.Mode = mode;
        agent.AddTerminal(new AgentTerminal("web1", _terminal) { Files = _files });
        agent.EventRaised += (_, e) => _events.Add(e);
        await agent.SendAsync("Turn on TLS", Token);
    }

    private Func<IReadOnlyList<ChatMessage>, ChatResponse> Record(Func<IReadOnlyList<ChatMessage>, ChatResponse> next) =>
        messages =>
        {
            _results.Add(LastToolResult(messages));
            return next(messages);
        };

    [Fact]
    public async Task Reads_a_file_exactly_as_data_and_says_so_in_the_chat()
    {
        await RunAsync(ApprovalMode.Ask, Tool("read_file", """{"terminal":"web1","path":"/etc/nginx/nginx.conf"}"""));

        Assert.Equal("<terminal_output>\n" + Config + "\n</terminal_output>", _results[0]);
        Assert.Contains(_events, e => e.Kind == AgentEventKind.Notice && e.Text.StartsWith("Read /etc/nginx/nginx.conf on web1", StringComparison.Ordinal));
        Assert.Empty(_terminal.Typed);
    }

    // ssh run in a local tab: file access would reach this computer, while typed commands reach the server.
    [Fact]
    public async Task Refuses_file_access_inside_a_session_opened_in_the_terminal()
    {
        _terminal.Session = "ssh session";

        await RunAsync(ApprovalMode.Ask, Tool("read_file", """{"terminal":"web1","path":"/etc/nginx/nginx.conf"}"""), Edit());

        Assert.All(_results.Take(2), result => Assert.Contains("ssh session", result, StringComparison.Ordinal));
        Assert.Empty(_terminal.Typed);
        Assert.Empty(_user.Proposals);
    }

    [Fact]
    public async Task Proposes_the_diff_then_backs_up_and_writes_in_the_terminal()
    {
        await RunAsync(ApprovalMode.Ask, Edit());

        var proposal = Assert.Single(_user.Proposals);
        Assert.Equal((ProposalKind.EditFile, "serve TLS"), (proposal.Kind, proposal.Purpose));
        Assert.Contains("-    listen 80;\n+    listen 443 ssl;\n", proposal.Command, StringComparison.Ordinal);
        Assert.Equal(Config.Replace("listen 80;", "listen 443 ssl;", StringComparison.Ordinal), _files.Staged["~/.datapoint-terminal/staging/1"]);
        var typed = Assert.Single(_terminal.Typed);
        Assert.Contains("cp -p -- '/etc/nginx/nginx.conf' ~/'.datapoint-terminal/backups/etc/nginx/nginx.conf.", typed, StringComparison.Ordinal);
        Assert.Contains("cat -- ~/'.datapoint-terminal/staging/1' > '/etc/nginx/nginx.conf'", typed, StringComparison.Ordinal);
        Assert.StartsWith("Backup: ~/.datapoint-terminal/backups/etc/nginx/nginx.conf.", _results[0], StringComparison.Ordinal);
        Assert.Contains("To undo: cp -- ~/'.datapoint-terminal/backups/etc/nginx/nginx.conf.", _results[0], StringComparison.Ordinal);
        Assert.Contains("Exit code: 0", _results[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Uses_sudo_when_the_file_is_not_writable()
    {
        _files.ReadOnly.Add("/etc/nginx/nginx.conf");

        await RunAsync(ApprovalMode.Ask, Edit());

        Assert.Contains("sudo tee -- '/etc/nginx/nginx.conf' < ~/'.datapoint-terminal/staging/1' > /dev/null", Assert.Single(_terminal.Typed), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Creates_a_new_file_without_a_backup()
    {
        await RunAsync(ApprovalMode.Ask, Tool("edit_file", """{"terminal":"web1","path":"~/notes.txt","content":"hello\n","purpose":"note"}"""));

        Assert.Equal("cat -- ~/'.datapoint-terminal/staging/1' > ~/'notes.txt' && rm -- ~/'.datapoint-terminal/staging/1'", Assert.Single(_terminal.Typed));
        Assert.StartsWith("Creates ~/notes.txt. To undo: rm -- ~/'notes.txt'", _results[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Does_not_write_a_declined_edit()
    {
        _user.Decide = proposal => CommandApproval.Reject(proposal.Command, "Keep port 80 too.");

        await RunAsync(ApprovalMode.Ask, Edit());

        Assert.Equal("Not changed: the user declined. They said: Keep port 80 too.", _results[0]);
        Assert.Empty(_terminal.Typed);
        Assert.Empty(_files.Staged);
    }

    [Fact]
    public async Task Does_not_overwrite_a_change_made_while_waiting()
    {
        _files.BeforeNextRead = () => _files.Files["/etc/nginx/nginx.conf"] = Config + "# edited by the user\n";

        await RunAsync(ApprovalMode.Ask, Edit());

        Assert.Equal("Not changed: the file changed while the edit waited. Read it again.", _results[0]);
        Assert.Empty(_terminal.Typed);
    }

    [Fact]
    public async Task Follows_the_plan_in_Plan_mode()
    {
        await RunAsync(ApprovalMode.Plan, Edit());

        Assert.StartsWith("Not changed: in Plan mode, show your plan", _results[0], StringComparison.Ordinal);
        Assert.Empty(_user.Proposals);
    }

    [Theory]
    [InlineData("listen 81;", "Not changed: The text to replace isn't in the file.")]
    [InlineData("listen 80;", "Not changed: the new text looks like it holds a secret.", "password ghp_0123456789abcdefghijABCDEFGHIJ012345;")]
    public async Task Refuses_edits_it_cannot_apply_before_asking(string old, string expected, string replacement = "x")
    {
        await RunAsync(ApprovalMode.Ask, Edit(old, replacement));

        Assert.StartsWith(expected, _results[0], StringComparison.Ordinal);
        Assert.Empty(_user.Proposals);
    }

    [Fact]
    public async Task Refuses_relative_paths_and_terminals_without_file_access()
    {
        await RunAsync(ApprovalMode.Ask, Edit(path: "nginx.conf"));

        Assert.StartsWith("Not changed: give an absolute path", _results[0], StringComparison.Ordinal);
    }
}
