namespace Datapoint.Terminal.Sessions.Ssh.Config;

internal sealed class ResolveState(string alias, string localUser)
{
    public string Alias { get; } = alias;

    public string LocalUser { get; } = localUser;

    public bool IsActive { get; set; } = true;

    public Dictionary<string, IReadOnlyList<string>> Options { get; } = new(StringComparer.Ordinal);

    public List<string> Problems { get; } = [];

    public string HostName => Options.TryGetValue("hostname", out var values) ? values[0] : Alias;

    public string User => Options.TryGetValue("user", out var values) ? values[0] : LocalUser;
}
