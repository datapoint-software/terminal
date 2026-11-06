namespace Datapoint.Terminal.Sessions.Ssh.Config;

// Read as OpenSSH reads it: for each option the first value that applies wins, except those that add up (e.g. IdentityFile).
// Files are read once, when loaded; Host, Match and Include are evaluated for each host, as ssh does.
public sealed class SshConfig
{
    private const int MaxIncludeDepth = 16;
    private const int MaxFileBytes = 1024 * 1024;

    private static readonly HashSet<string> s_cumulative = new(StringComparer.Ordinal)
    {
        "identityfile", "certificatefile", "localforward", "remoteforward", "dynamicforward", "sendenv",
    };

    private readonly Dictionary<string, IReadOnlyList<SshConfigLine>> _files;
    private readonly string _main;
    private readonly string _sshFolder;
    private readonly string _home;
    private readonly List<string> _loadProblems = [];

    private SshConfig(string main, string sshFolder, string home, Dictionary<string, IReadOnlyList<SshConfigLine>> files)
    {
        _main = main;
        _sshFolder = sshFolder;
        _home = home;
        _files = files;
    }

    public static string DefaultPath => Path.Combine(Home, ".ssh", "config");

    public IReadOnlyList<string> Problems => _loadProblems;

    private static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static SshConfig Load() => Load(DefaultPath, Home);

    public static SshConfig Load(string path, string home)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(home);
        var config = new SshConfig(path, Path.GetDirectoryName(path)!, home, new Dictionary<string, IReadOnlyList<SshConfigLine>>(StringComparer.Ordinal));
        config.ReadFile(path, depth: 0);
        return config;
    }

    public IReadOnlyList<string> HostAliases()
    {
        var aliases = new List<string>();
        CollectAliases(_main, aliases, depth: 0);
        return [.. aliases.Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    public SshHostConfig Resolve(string alias, string? localUser = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        var state = new ResolveState(alias, localUser ?? Environment.UserName);
        Evaluate(_main, state, depth: 0);
        return new SshHostConfig(alias, state.LocalUser, _home, state.Options, [.. _loadProblems, .. state.Problems]);
    }

    private static SshConfigLine? ParseLine(string text, string source, int number)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0 || trimmed[0] == '#')
        {
            return null;
        }

        // "Keyword value", "Keyword=value" and "Keyword = value" are all allowed.
        var end = trimmed.AsSpan().IndexOfAny(" \t=");
        var keyword = end < 0 ? trimmed : trimmed[..end];
        var rest = end < 0 ? string.Empty : trimmed[end..].TrimStart();
        rest = rest.StartsWith('=') ? rest[1..] : rest;
        return new SshConfigLine(keyword.ToLowerInvariant(), SshArguments.Split(rest), source, number);
    }

    private void ReadFile(string path, int depth)
    {
        if (_files.ContainsKey(path) || depth > MaxIncludeDepth)
        {
            return;
        }

        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                _files[path] = [];
                return;
            }

            if (info.Length > MaxFileBytes)
            {
                _loadProblems.Add($"{path} is larger than 1 MB and was skipped.");
                _files[path] = [];
                return;
            }

            var lines = File.ReadAllLines(path)
                .Select((text, index) => ParseLine(text, path, index + 1))
                .OfType<SshConfigLine>()
                .ToList();
            _files[path] = lines;
            foreach (var include in lines.Where(line => string.Equals(line.Keyword, "include", StringComparison.Ordinal)))
            {
                foreach (var file in include.Arguments.SelectMany(IncludedFiles))
                {
                    ReadFile(file, depth + 1);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _loadProblems.Add($"{path} couldn't be read: {exception.Message}");
            _files[path] = [];
        }
    }

    // Relative paths are in ~/.ssh; wildcards are allowed in the file name, and matching files are read in name order.
    private IEnumerable<string> IncludedFiles(string argument)
    {
        var expanded = argument.StartsWith('~') ? _home + argument[1..] : argument;
        var full = Path.IsPathRooted(expanded) ? expanded : Path.Combine(_sshFolder, expanded);
        var folder = Path.GetDirectoryName(full);
        var name = Path.GetFileName(full);
        if (folder is null || !SshPattern.HasWildcard(name))
        {
            return [full];
        }

        if (!Directory.Exists(folder))
        {
            return [];
        }

        return Directory.EnumerateFiles(folder)
            .Where(file => SshPattern.IsMatch(Path.GetFileName(file), name))
            .Order(StringComparer.Ordinal);
    }

    private void CollectAliases(string path, List<string> aliases, int depth)
    {
        if (depth > MaxIncludeDepth || !_files.TryGetValue(path, out var lines))
        {
            return;
        }

        foreach (var line in lines)
        {
            if (string.Equals(line.Keyword, "host", StringComparison.Ordinal))
            {
                aliases.AddRange(line.Arguments.Where(pattern => !SshPattern.HasWildcard(pattern)));
            }
            else if (string.Equals(line.Keyword, "include", StringComparison.Ordinal))
            {
                foreach (var file in line.Arguments.SelectMany(IncludedFiles))
                {
                    CollectAliases(file, aliases, depth + 1);
                }
            }
        }
    }

    private void Evaluate(string path, ResolveState state, int depth)
    {
        if (depth > MaxIncludeDepth || !_files.TryGetValue(path, out var lines))
        {
            return;
        }

        foreach (var line in lines)
        {
            switch (line.Keyword)
            {
                case "host":
                    state.IsActive = SshPattern.IsListMatch(state.Alias, line.Arguments);
                    break;
                case "match":
                    state.IsActive = SshMatch.Evaluate(line, state);
                    break;
                case "include" when state.IsActive:
                    foreach (var file in line.Arguments.SelectMany(IncludedFiles))
                    {
                        Evaluate(file, state, depth + 1);
                    }

                    // A Host or Match line inside the included file ends with it.
                    state.IsActive = true;
                    break;
                case "include":
                    break;
                default:
                    Apply(line, state);
                    break;
            }
        }
    }

    private static void Apply(SshConfigLine line, ResolveState state)
    {
        if (!state.IsActive || line.Arguments.Count == 0)
        {
            return;
        }

        var value = string.Join(' ', line.Arguments);
        if (s_cumulative.Contains(line.Keyword))
        {
            var existing = state.Options.TryGetValue(line.Keyword, out var values) ? values : [];
            state.Options[line.Keyword] = [.. existing, value];
            return;
        }

        state.Options.TryAdd(line.Keyword, [value]);
    }
}
