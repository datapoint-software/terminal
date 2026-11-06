using System.Globalization;

namespace Datapoint.Terminal.Sessions.Ssh.Config;

public sealed class SshHostConfig
{
    private readonly Dictionary<string, IReadOnlyList<string>> _options;

    internal SshHostConfig(string alias, string localUser, string home, Dictionary<string, IReadOnlyList<string>> options, IReadOnlyList<string> problems)
    {
        Alias = alias;
        _options = options;
        Problems = problems;
        HostName = Expand(Get("hostname") ?? alias, alias, alias, string.Empty, localUser, home);
        User = Get("user") ?? localUser;
        Port = int.TryParse(Get("port"), NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port is > 0 and < 65536 ? port : 22;
        IdentityFiles = [.. GetAll("identityfile").Select(file => Expand(file, alias, HostName, User, localUser, home))];
    }

    public string Alias { get; }

    public string HostName { get; }

    public string User { get; }

    public int Port { get; }

    public IReadOnlyList<string> IdentityFiles { get; }

    public IReadOnlyList<string> Problems { get; }

    public IReadOnlyCollection<string> Keywords => _options.Keys;

    public string? Get(string keyword) => _options.TryGetValue(keyword.ToLowerInvariant(), out var values) ? values[0] : null;

    public IReadOnlyList<string> GetAll(string keyword) => _options.TryGetValue(keyword.ToLowerInvariant(), out var values) ? values : [];

    public bool IsYes(string keyword) => string.Equals(Get(keyword), "yes", StringComparison.OrdinalIgnoreCase);

    // The tokens OpenSSH expands in file names: %d home, %h host name, %n alias, %r remote user, %u and %l local user and
    // host, %% a percent sign. A leading ~ is the home folder.
    internal static string Expand(string value, string alias, string hostName, string remoteUser, string localUser, string home)
    {
        var text = value.StartsWith("~/", StringComparison.Ordinal) || string.Equals(value, "~", StringComparison.Ordinal)
            ? home + value[1..]
            : value;
        if (!text.Contains('%', StringComparison.Ordinal))
        {
            return text;
        }

        var builder = new System.Text.StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '%' || index + 1 == text.Length)
            {
                builder.Append(text[index]);
                continue;
            }

            index++;
            builder.Append(text[index] switch
            {
                '%' => "%",
                'd' => home,
                'h' => hostName,
                'n' => alias,
                'r' => remoteUser,
                'u' => localUser,
                'l' => Environment.MachineName,
                _ => "%" + text[index],
            });
        }

        return builder.ToString();
    }
}
