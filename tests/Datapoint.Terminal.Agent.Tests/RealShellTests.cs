using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Agent.Tests.Fakes;
using Datapoint.Terminal.Sessions;
using Datapoint.Terminal.Sessions.Local;
using Microsoft.Extensions.AI;
using static Datapoint.Terminal.Agent.Tests.Fakes.ScriptedChatClient;

namespace Datapoint.Terminal.Agent.Tests;

public sealed class RealShellTests : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("datapoint-agent-").FullName;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(_home, recursive: true);

    private async Task<TerminalConnection> StartBashAsync()
    {
        if (OperatingSystem.IsWindows() || !File.Exists("/bin/bash"))
        {
            Assert.Skip("bash isn't installed here.");
        }

        var session = await LocalSession.StartAsync(
            new LocalSessionOptions
            {
                Shell = "/bin/bash",
                Login = false,
                WorkingDirectory = _home,
                Columns = 100,
                Rows = 30,
                Environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["HOME"] = _home, ["PS1"] = "$ " },
            },
            Token);
        var connection = new TerminalConnection(session, 100, 30);
        for (var attempt = 0; attempt < 200 && !connection.Terminal.IsAtPrompt; attempt++)
        {
            await Task.Delay(50, Token);
        }

        return connection;
    }

    [Fact]
    public async Task Runs_commands_in_bash_and_reads_their_output_and_exit_codes()
    {
        await using var connection = await StartBashAsync();
        var results = new List<string>();
        var client = new ScriptedChatClient(
            _ => Call("run_command", new Dictionary<string, object?>(StringComparer.Ordinal) { ["command"] = "printf 'one\\ntwo\\n'", ["purpose"] = "test" }),
            messages => { results.Add(LastToolResult(messages)); return Call("run_command", new Dictionary<string, object?>(StringComparer.Ordinal) { ["command"] = "ls /nonexistent-folder", ["purpose"] = "test" }, "call-2"); },
            messages => { results.Add(LastToolResult(messages)); return Text("Done."); });
        using var agent = new TerminalAgent(client, new ConnectionTerminal(connection), new FakeUser(), new SecretRedactor(), new AgentOptions { CommandTimeout = TimeSpan.FromSeconds(10) });

        await agent.SendAsync("Try it", Token);

        Assert.Equal("Exit code: 0\n<terminal_output>\none\ntwo\n</terminal_output>", results[0]);
        Assert.StartsWith("Exit code: ", results[1], StringComparison.Ordinal);
        Assert.DoesNotContain("Exit code: 0", results[1], StringComparison.Ordinal);
        Assert.Contains("No such file", results[1], StringComparison.Ordinal);
        Assert.Contains(connection.Terminal.Commands, command => command.Source == Emulator.ShellIntegration.CommandSource.Agent && string.Equals(command.CommandLine, "ls /nonexistent-folder", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Reports_a_long_command_as_still_running_and_waits_for_it()
    {
        await using var connection = await StartBashAsync();
        var results = new List<string>();
        var client = new ScriptedChatClient(
            _ => Call("run_command", new Dictionary<string, object?>(StringComparer.Ordinal) { ["command"] = "sleep 1; echo finished", ["purpose"] = "test" }),
            messages => { results.Add(LastToolResult(messages)); return Call("wait_for_command", new Dictionary<string, object?>(StringComparer.Ordinal), "call-2"); },
            messages => { results.Add(LastToolResult(messages)); return Text("Done."); });
        using var agent = new TerminalAgent(client, new ConnectionTerminal(connection), new FakeUser(), new SecretRedactor(), new AgentOptions { CommandTimeout = TimeSpan.FromMilliseconds(700) });

        await agent.SendAsync("Try it", Token);

        Assert.StartsWith("Still running", results[0], StringComparison.Ordinal);
        Assert.Equal("Exit code: 0\n<terminal_output>\nfinished\n</terminal_output>", results[1]);
    }
}
