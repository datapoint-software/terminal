namespace Datapoint.Terminal.Agent;

public interface ITerminalOpener
{
    IReadOnlyList<AgentConnection> Connections { get; }

    // The new terminal's name, or null when it couldn't be opened; the application tells the user why.
    Task<string?> OpenAsync(string connection, CancellationToken cancellationToken);

    // Opens a terminal again for a host whose terminal went away, e.g. while it rebooted during a rollout. Tried again and again,
    // so a failure leaves no notice; the new terminal's name, or null.
    Task<string?> ReconnectAsync(string connection, CancellationToken cancellationToken);
}
