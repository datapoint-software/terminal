using System.Globalization;
using Datapoint.Terminal.Agent.Workspaces;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Shell;

// What the Add a Host sheet asks for, turned into a connection for workspace.json, or the first thing to correct. Given a host
// to edit, it starts from that host's values and keeps its name, which is also the folder its documentation lives in.
public sealed class NewHostForm
{
    private readonly IReadOnlyCollection<WorkspaceConnection> _existing;
    private readonly IReadOnlyCollection<string> _sshAliases;

    public NewHostForm(IReadOnlyCollection<WorkspaceConnection> existing, IReadOnlyCollection<string> sshAliases, WorkspaceConnection? editing = null)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(sshAliases);
        _existing = editing is null ? existing : [.. existing.Where(connection => !string.Equals(connection.Name, editing.Name, StringComparison.OrdinalIgnoreCase))];
        _sshAliases = sshAliases;
        Editing = editing;
        UsedLabels = [.. existing
            .SelectMany(connection => connection.Labels ?? [])
            .GroupBy(label => label, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())];
        if (editing is null)
        {
            return;
        }

        Name = editing.Name;
        Address = AddressOf(editing);
        User = editing.User ?? string.Empty;
        Port = editing.Port?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        Labels = string.Join(", ", editing.Labels ?? []);
        if (editing.Environment is { } environment && IndexOf(environment) < 0)
        {
            Choices = [.. Environments, environment];
        }

        EnvironmentIndex = IndexOf(editing.Environment);
    }

    // The environments offered, plus one written by hand in workspace.json (e.g. "qa"), so saving an edit doesn't drop it.
    public IReadOnlyList<string?> Choices { get; } = Environments;

    // The one chosen, as it's written in workspace.json.
    public string? Environment => Choices.ElementAtOrDefault(EnvironmentIndex);

    public WorkspaceConnection? Editing { get; }

    public bool IsEditing => Editing is not null;

    // Lowercase, as the labels are written in workspace.json; the order matches the segmented control.
    public static IReadOnlyList<string?> Environments { get; } = [null, "development", "staging", "production"];

    public string Name { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string User { get; set; } = string.Empty;

    public string Port { get; set; } = string.Empty;

    public int EnvironmentIndex { get; set; }

    // Comma-separated, as typed.
    public string Labels { get; set; } = string.Empty;

    // The labels other hosts in the workspace already have, most used first, so the same label is spelled the same way.
    public IReadOnlyList<string> UsedLabels { get; }

    // Adds a suggested label to what's typed, unless it's already there.
    public void AddLabel(string label)
    {
        var labels = SplitLabels(Labels);
        if (labels.Contains(label, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        Labels = string.Join(", ", [.. labels, label]);
    }

    public bool IsProduction => Agent.EnvironmentLabels.IsProduction(Environment);

    // Production makes the agent ask before every command, so taking it away is worth a word before saving.
    public bool IsLeavingProduction => Editing is not null && Agent.EnvironmentLabels.IsProduction(Editing.Environment) && !IsProduction;

    // The host's document, log and the agent's notes are kept under its name, so they stay put when the address changes.
    public bool IsMoving => Editing is not null && !string.Equals(Address.Trim(), AddressOf(Editing), StringComparison.OrdinalIgnoreCase);

    // An alias from the SSH config: SSH already knows its address, user and port, so only the name is filled in.
    public void UseAlias(string alias)
    {
        ArgumentNullException.ThrowIfNull(alias);
        Address = alias;
        if (IsEditing || (Name.Length != 0 && !IsAlias(Name)))
        {
            return;
        }

        Name = alias;
    }

    public (WorkspaceConnection? Connection, string? Problem) Build()
    {
        var name = Editing?.Name ?? Name.Trim();
        var address = Address.Trim();
        var alias = AliasFor(address);
        if ((NameProblem(name) ?? AddressProblem(alias, address) ?? UserProblem(User.Trim())) is { } problem)
        {
            return (null, problem);
        }

        if (!TryReadPort(Port.Trim(), out var port))
        {
            return (null, Strings.NewHostPortProblem);
        }

        var labels = SplitLabels(Labels);
        if (LabelsProblem(labels) is { } labelsProblem)
        {
            return (null, labelsProblem);
        }

        var connection = Connection(name, alias, address, port, labels);
        return connection.Problem() is { } wrong ? (null, wrong) : (connection, null);
    }

    // An edit keeps what the sheet doesn't show, such as the host's model, container and forwarded ports.
    private WorkspaceConnection Connection(string name, string? alias, string address, int? port, List<string> labels) =>
        (Editing ?? new WorkspaceConnection { Name = name }) with
        {
            SshHost = alias,
            Host = alias is null ? address : null,
            User = User.Trim() is { Length: > 0 } user ? user : null,
            Port = port,
            Environment = Environment,
            Labels = labels.Count == 0 ? null : labels,
        };

    private string? NameProblem(string name) =>
        !WorkspaceConnection.IsPlainLabel(name) ? Strings.NewHostNameProblem
        : _existing.Any(connection => string.Equals(connection.Name, name, StringComparison.OrdinalIgnoreCase)) ? Strings.Format(Strings.NewHostNameTaken, name)
        : null;

    private static string? LabelsProblem(List<string> labels) =>
        labels.Count > WorkspaceConnection.MostLabels || labels.Any(label => !WorkspaceConnection.IsPlainLabel(label)) ? Strings.NewHostLabelsProblem : null;

    private static string? AddressProblem(string? alias, string address) => alias is null && !IsHostName(address) ? Strings.NewHostAddressProblem : null;

    private static string? UserProblem(string user) =>
        user.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_') ? null : Strings.NewHostUserProblem;

    private static bool TryReadPort(string text, out int? port)
    {
        port = null;
        if (text.Length == 0)
        {
            return true;
        }

        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number is < 1 or > 65535)
        {
            return false;
        }

        port = number;
        return true;
    }

    // A label written the way another host has it takes that spelling.
    private List<string> SplitLabels(string text) =>
    [
        .. text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(label => UsedLabels.FirstOrDefault(used => string.Equals(used, label, StringComparison.OrdinalIgnoreCase)) ?? label)
            .Distinct(StringComparer.OrdinalIgnoreCase),
    ];

    // An unchanged address keeps how it was written, even if the alias has since left the SSH config.
    private string? AliasFor(string address) =>
        Editing is { } host && !IsMoving ? host.SshHost : _sshAliases.FirstOrDefault(known => string.Equals(known, address, StringComparison.OrdinalIgnoreCase));

    private bool IsAlias(string text) => _sshAliases.Contains(text, StringComparer.OrdinalIgnoreCase);

    private static string AddressOf(WorkspaceConnection connection) => connection.SshHost ?? connection.Host ?? string.Empty;

    private int IndexOf(string? environment) =>
        Choices.ToList().FindIndex(item => string.Equals(item, environment, StringComparison.OrdinalIgnoreCase));

    // Only names and addresses: anything with spaces, options or a user@ in it is the kind of text that could make ssh do
    // something else, and it's written to a shared file.
    private static bool IsHostName(string address) =>
        address.Length is > 0 and <= 253 && !address.StartsWith('-') && Uri.CheckHostName(address.Trim('[', ']')) is not UriHostNameType.Unknown;
}
