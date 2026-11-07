using System.Globalization;
using Datapoint.Terminal.Agent.Projects;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Shell;

// What the Add a Host sheet asks for, turned into a connection for .terminal.json, or the first thing to correct.
public sealed class NewHostForm(IReadOnlyCollection<ProjectConnection> existing, IReadOnlyCollection<string> sshAliases)
{
    // Lowercase, as the labels are written in .terminal.json; the order matches the segmented control.
    public static IReadOnlyList<string?> Environments { get; } = [null, "development", "staging", "production"];

    public string Name { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string User { get; set; } = string.Empty;

    public string Port { get; set; } = string.Empty;

    public int EnvironmentIndex { get; set; }

    public bool IsProduction => Agent.EnvironmentLabels.IsProduction(Environments.ElementAtOrDefault(EnvironmentIndex));

    // An alias from the SSH config: SSH already knows its address, user and port, so only the name is filled in.
    public void UseAlias(string alias)
    {
        ArgumentNullException.ThrowIfNull(alias);
        Address = alias;
        if (Name.Length != 0 && !IsAlias(Name))
        {
            return;
        }

        Name = alias;
    }

    public (ProjectConnection? Connection, string? Problem) Build()
    {
        var name = Name.Trim();
        var address = Address.Trim();
        var alias = sshAliases.FirstOrDefault(known => string.Equals(known, address, StringComparison.OrdinalIgnoreCase));
        if ((NameProblem(name) ?? AddressProblem(alias, address) ?? UserProblem(User.Trim())) is { } problem)
        {
            return (null, problem);
        }

        if (!TryReadPort(Port.Trim(), out var port))
        {
            return (null, Strings.NewHostPortProblem);
        }

        var connection = new ProjectConnection
        {
            Name = name,
            SshHost = alias,
            Host = alias is null ? address : null,
            User = User.Trim() is { Length: > 0 } user ? user : null,
            Port = port,
            Environment = Environments.ElementAtOrDefault(EnvironmentIndex),
        };
        return connection.Problem() is { } wrong ? (null, wrong) : (connection, null);
    }

    private string? NameProblem(string name) =>
        !ProjectConnection.IsPlainLabel(name) ? Strings.NewHostNameProblem
        : existing.Any(connection => string.Equals(connection.Name, name, StringComparison.OrdinalIgnoreCase)) ? Strings.Format(Strings.NewHostNameTaken, name)
        : null;

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

    private bool IsAlias(string text) => sshAliases.Contains(text, StringComparer.OrdinalIgnoreCase);

    // Only names and addresses: anything with spaces, options or a user@ in it is the kind of text that could make ssh do
    // something else, and it's written to a shared file.
    private static bool IsHostName(string address) =>
        address.Length is > 0 and <= 253 && !address.StartsWith('-') && Uri.CheckHostName(address.Trim('[', ']')) is not UriHostNameType.Unknown;
}
