using Datapoint.Terminal.Agent.Memory;
using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Agent.Tests.Fakes;
using Datapoint.Terminal.Agent.Workspaces;
using Microsoft.Extensions.AI;
using static Datapoint.Terminal.Agent.Tests.Fakes.ScriptedChatClient;

namespace Datapoint.Terminal.Agent.Tests;

public sealed class TerminalAgentMemoryTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("agent-memory-").FullName;
    private readonly FakeUser _user = new();
    private readonly List<AgentEventArgs> _events = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string Global => Path.Combine(_folder, "global", MemoryFile.FileName);

    private string WorkspacePath => Path.Combine(_folder, "workspace", MemoryFile.FileName);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static Func<IReadOnlyList<ChatMessage>, ChatResponse> Tool(string name, string scope, string note) =>
        _ => Call(name, new Dictionary<string, object?>(StringComparer.Ordinal) { ["scope"] = scope, ["note"] = note });

    private async Task<string?> RunAsync(Func<IReadOnlyList<ChatMessage>, ChatResponse> call, AgentMemory? memory = null)
    {
        string? seen = null;
        var client = new ScriptedChatClient(call, messages => { seen = LastToolResult(messages); return Text("OK."); });
        using var agent = new TerminalAgent(client, _user, new SecretRedactor()) { Memory = memory ?? new AgentMemory(Global, WorkspacePath) };
        agent.EventRaised += (_, e) => _events.Add(e);
        await agent.SendAsync("From now on, use doas", Token);
        return seen;
    }

    [Fact]
    public async Task Remembers_a_note_and_says_so_in_the_chat()
    {
        var result = await RunAsync(Tool("remember", "workspace", "Use  doas,\nnot sudo"));

        Assert.Equal("Remembered.", result);
        Assert.Equal(["Use doas, not sudo"], MemoryFile.Notes(WorkspacePath), StringComparer.Ordinal);
        Assert.False(File.Exists(Global));
        var change = Assert.Single(_events, e => e.Kind == AgentEventKind.MemoryChanged);
        Assert.Equal(WorkspacePath, change.Path);
        Assert.Equal("Remembered for this workspace: Use doas, not sudo", change.Text);
    }

    [Fact]
    public async Task Refuses_a_note_that_looks_like_a_secret()
    {
        var result = await RunAsync(Tool("remember", "global", "The database password=hunter22 is in the vault"));

        Assert.StartsWith("Not remembered: the note looks like it holds a secret", result, StringComparison.Ordinal);
        Assert.False(File.Exists(Global));
        Assert.DoesNotContain(_events, e => e.Kind == AgentEventKind.MemoryChanged);
    }

    [Fact]
    public async Task Refuses_a_long_note()
    {
        var result = await RunAsync(Tool("remember", "global", new string('x', MemoryFile.MaxNoteLength + 1)));

        Assert.StartsWith("Not changed: keep a note under", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Does_not_write_the_workspace_memory_from_a_read_only_window()
    {
        var result = await RunAsync(Tool("remember", "workspace", "Use doas"), new AgentMemory(Global, WorkspacePath: null));

        Assert.Equal("Not changed: this window can't write the workspace's memory (it's open read-only).", result);
        Assert.False(File.Exists(WorkspacePath));
    }

    [Fact]
    public async Task Forgets_a_note()
    {
        MemoryFile.Add(Global, "Use doas");

        var result = await RunAsync(Tool("forget", "global", "Use doas"));

        Assert.Equal("Forgotten.", result);
        Assert.Empty(MemoryFile.Notes(Global));
        Assert.Contains(_events, e => e.Kind == AgentEventKind.MemoryChanged && string.Equals(e.Text, "Forgot everywhere: Use doas", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Sends_memory_ahead_of_the_instruction_files()
    {
        var client = new ScriptedChatClient(_ => Text("OK."));
        using var agent = new TerminalAgent(client, _user, new SecretRedactor())
        {
            InstructionsSource = _ => Task.FromResult(new AgentInstructions([new InstructionFile("/home/me/TERMINAL.md", "Be brief.", IsCut: false)], [])
            {
                Memory = [new InstructionFile("/home/me/MEMORY.md", "- Use doas", IsCut: false)],
            }),
        };

        await agent.SendAsync("Hello", Token);

        var system = client.Requests[0][0].Text;
        var memory = system.IndexOf("- Use doas", StringComparison.Ordinal);
        Assert.True(memory > 0);
        Assert.True(memory < system.IndexOf("Be brief.", StringComparison.Ordinal));
        Assert.Contains("the instruction files win", system, StringComparison.Ordinal);
    }
}
