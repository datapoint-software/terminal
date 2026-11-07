namespace Datapoint.Terminal.Agent;

public interface ITerminalOpener
{
    IReadOnlyList<AgentConnection> Connections { get; }

    // The new terminal's name, or null when it couldn't be opened; the application tells the user why.
    Task<string?> OpenAsync(string connection, CancellationToken cancellationToken);
}
