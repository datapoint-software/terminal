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
        using var agent = new TerminalAgent(client, new FakeUser(), new SecretRedactor(), new AgentOptions { CommandTimeout = TimeSpan.FromSeconds(10) });
        agent.AddTerminal(new AgentTerminal("Local", new ConnectionTerminal(connection)));

        await agent.SendAsync("Try it", Token);

        Assert.Equal("Exit code: 0\n<terminal_output>\none\ntwo\n</terminal_output>", results[0]);
        Assert.StartsWith("Exit code: ", results[1], StringComparison.Ordinal);
        Assert.DoesNotContain("Exit code: 0", results[1], StringComparison.Ordinal);
        Assert.Contains("No such file", results[1], StringComparison.Ordinal);
        Assert.Contains(connection.Terminal.Commands, command => command.Source == Emulator.ShellIntegration.CommandSource.Agent && string.Equals(command.CommandLine, "ls /nonexistent-folder", StringComparison.Ordinal));
    }

    // The user started another shell, as with ssh: the outer shell reports it as a running command, yet the agent types into it.
    [Fact]
    public async Task Types_into_a_session_the_user_opened_in_the_terminal()
    {
        await using var connection = await StartBashAsync();
        // A plain prompt: the test exports PS1, which would otherwise carry this shell's prompt marks into the inner one.
        connection.Send("PS1='$ ' sh\r"u8.ToArray());
        for (var attempt = 0; attempt < 100 && connection.Terminal.CurrentCommand is not { IsRunning: true }; attempt++)
        {
            await Task.Delay(20, Token);
        }

        await Task.Delay(300, Token);
        var results = new List<string>();
        var client = new ScriptedChatClient(
            _ => Call("run_command", new Dictionary<string, object?>(StringComparer.Ordinal) { ["command"] = "echo inside-$((20+22))", ["purpose"] = "test" }),
            messages => { results.Add(LastToolResult(messages)); return Text("Done."); });
        using var agent = new TerminalAgent(client, new FakeUser(), new SecretRedactor(), new AgentOptions { CommandTimeout = TimeSpan.FromSeconds(10) });
        var terminal = new ConnectionTerminal(connection);
        agent.AddTerminal(new AgentTerminal("Local", terminal));

        Assert.Equal("nested shell", terminal.GetState().Session);
        await agent.SendAsync("Try it", Token);

        Assert.Contains("inside-42", results[0], StringComparison.Ordinal);
        Assert.DoesNotContain("still running", results[0], StringComparison.Ordinal);
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
        using var agent = new TerminalAgent(client, new FakeUser(), new SecretRedactor(), new AgentOptions { CommandTimeout = TimeSpan.FromMilliseconds(700) });
        agent.AddTerminal(new AgentTerminal("Local", new ConnectionTerminal(connection)));

        await agent.SendAsync("Try it", Token);

        Assert.StartsWith("Still running", results[0], StringComparison.Ordinal);
        Assert.Equal("Exit code: 0\n<terminal_output>\nfinished\n</terminal_output>", results[1]);
    }
}
