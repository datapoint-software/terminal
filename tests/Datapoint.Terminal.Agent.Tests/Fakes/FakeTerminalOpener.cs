namespace Datapoint.Terminal.Agent.Tests.Fakes;

// Opens a fake terminal and gives it to the agent, as the window does.
internal sealed class FakeTerminalOpener(IReadOnlyList<AgentConnection> connections) : ITerminalOpener
{
    public TerminalAgent? Agent { get; set; }

    public List<string> Opened { get; } = [];

    public bool Fails { get; set; }

    public IReadOnlyList<AgentConnection> Connections => connections;

    public Task<string?> OpenAsync(string connection, CancellationToken cancellationToken)
    {
        if (Fails)
        {
            return Task.FromResult<string?>(null);
        }

        Opened.Add(connection);
        var match = connections.First(item => string.Equals(item.Name, connection, StringComparison.Ordinal));
        Agent!.AddTerminal(new AgentTerminal(connection, new FakeTerminal(), match.Mode, match.Environment));
        return Task.FromResult<string?>(connection);
    }
}
