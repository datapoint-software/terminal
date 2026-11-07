namespace Datapoint.Terminal.Agent.Workspaces;

// Product decision 92: changes to .terminal.json from outside the application may make a connection stricter on their own,
// never looser. Looser or redirecting changes are held, and the connection keeps its confirmed value until the user accepts.
public sealed record ConnectionReview(IReadOnlyList<WorkspaceConnection> Effective, IReadOnlyList<ConnectionChange> Held)
{
    public const string NewConnection = "connection";
    public const string EnvironmentSetting = "environment";
    public const string ApprovalSetting = "approval mode";
    public const string AddressSetting = "address";

    // The effective list is also the new confirmed record: stricter changes and removals are accepted as they are.
    public static ConnectionReview Compare(IReadOnlyList<WorkspaceConnection> current, IReadOnlyList<WorkspaceConnection> confirmed, ApprovalMode defaultMode)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(confirmed);
        var effective = new List<WorkspaceConnection>();
        var held = new List<ConnectionChange>();
        foreach (var connection in current)
        {
            var previous = confirmed.FirstOrDefault(item => string.Equals(item.Name, connection.Name, StringComparison.OrdinalIgnoreCase));
            if (previous is null)
            {
                held.Add(new ConnectionChange(connection.Name, NewConnection, null, Address(connection)));
                continue;
            }

            effective.Add(Merge(connection, previous, defaultMode, held));
        }

        return new ConnectionReview(effective, held);
    }

    // Whether two records agree on everything this review guards: the label, the approval mode and the address.
    public static bool IsSameGuarded(WorkspaceConnection first, WorkspaceConnection second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        return string.Equals(first.Environment, second.Environment, StringComparison.Ordinal)
            && first.ApprovalMode == second.ApprovalMode
            && string.Equals(Address(first), Address(second), StringComparison.Ordinal);
    }

    // A changed container counts as a changed address: it decides where the agent's commands run. So do forwarded ports, which
    // open a way from this computer into the host's network.
    public static string Address(WorkspaceConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var container = (connection.Container is { } target ? (connection.IsLocal ? string.Empty : " ") + target.Address() : string.Empty)
            + string.Concat((connection.Forwards ?? []).Select(forward => " -L " + forward));
        if (connection.SshHost is { } alias)
        {
            return alias + container;
        }

        var user = connection.User is null ? string.Empty : connection.User + "@";
        var port = connection.Port is { } number ? ":" + number.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
        return user + connection.Host + port + container;
    }

    private static WorkspaceConnection Merge(WorkspaceConnection connection, WorkspaceConnection previous, ApprovalMode defaultMode, List<ConnectionChange> held)
    {
        var merged = connection;
        if (!string.Equals(connection.Environment, previous.Environment, StringComparison.Ordinal) && !(connection.IsProduction && !previous.IsProduction))
        {
            held.Add(new ConnectionChange(connection.Name, EnvironmentSetting, previous.Environment, connection.Environment));
            merged = merged with { Environment = previous.Environment };
        }

        if (Strictness(merged, defaultMode) < Strictness(previous, defaultMode))
        {
            held.Add(new ConnectionChange(connection.Name, ApprovalSetting, Mode(previous), Mode(merged)));
            merged = merged with { ApprovalMode = previous.ApprovalMode };
        }

        if (!string.Equals(Address(connection), Address(previous), StringComparison.Ordinal))
        {
            held.Add(new ConnectionChange(connection.Name, AddressSetting, Address(previous), Address(connection)));
            merged = merged with { SshHost = previous.SshHost, Host = previous.Host, User = previous.User, Port = previous.Port, Container = previous.Container, Forwards = previous.Forwards };
        }

        return merged;
    }

    // A connection without its own mode takes the application's default, or Ask when it's labeled production.
    private static ApprovalMode ModeOf(WorkspaceConnection connection, ApprovalMode defaultMode) =>
        connection.ApprovalMode ?? (connection.IsProduction ? ApprovalMode.Ask : defaultMode);

    private static int Strictness(WorkspaceConnection connection, ApprovalMode defaultMode) => ModeOf(connection, defaultMode) switch
    {
        ApprovalMode.Ask => 2,
        ApprovalMode.AskForRisky => 1,
        _ => 0,
    };

    private static string? Mode(WorkspaceConnection connection) => connection.ApprovalMode?.ToString();
}
