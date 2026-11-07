namespace Datapoint.Terminal.Agent.Tests.Fakes;

// Opens a fake terminal and gives it to the agent, as the window does.
internal sealed class FakeTerminalOpener(IReadOnlyList<AgentConnection> connections) : ITerminalOpener
{
    public TerminalAgent? Agent { get; set; }

    public List<string> Opened { get; } = [];

    public bool Fails { get; set; }

    // Attempts that fail before a reconnect succeeds, as while a host reboots.
    public int FailedReconnects { get; set; }

    public List<string> Reconnects { get; } = [];

    public Func<string, FakeTerminal> NewTerminal { get; set; } = _ => new FakeTerminal();

    public IReadOnlyList<AgentConnection> Connections => connections;

    public Task<string?> OpenAsync(string connection, CancellationToken cancellationToken)
    {
        if (Fails)
        {
            return Task.FromResult<string?>(null);
        }

        Opened.Add(connection);
        var match = connections.First(item => string.Equals(item.Name, connection, StringComparison.Ordinal));
        Agent!.AddTerminal(new AgentTerminal(connection, NewTerminal(connection), match.Environment) { Connection = connection });
        return Task.FromResult<string?>(connection);
    }

    public Task<string?> ReconnectAsync(string connection, CancellationToken cancellationToken)
    {
        Reconnects.Add(connection);
        if (Reconnects.Count <= FailedReconnects)
        {
            return Task.FromResult<string?>(null);
        }

        return OpenAsync(connection, cancellationToken);
    }
}
