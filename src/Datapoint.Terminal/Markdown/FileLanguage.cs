namespace Datapoint.Terminal.Markdown;

// The language a file is written in, from its name and folder, for colouring it in the editor. Configuration files often have
// no telling extension, so the folder counts too: a .conf under nginx/ is nginx's.
internal static class FileLanguage
{
    private static readonly Dictionary<string, (string Name, string Label)> s_byExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".sh"] = ("sh", "Shell"),
        [".bash"] = ("sh", "Shell"),
        [".zsh"] = ("sh", "Shell"),
        [".py"] = ("python", "Python"),
        [".js"] = ("js", "JavaScript"),
        [".mjs"] = ("js", "JavaScript"),
        [".ts"] = ("ts", "TypeScript"),
        [".go"] = ("go", "Go"),
        [".rs"] = ("rust", "Rust"),
        [".c"] = ("c", "C"),
        [".h"] = ("c", "C"),
        [".cs"] = ("cs", "C#"),
        [".java"] = ("java", "Java"),
        [".php"] = ("php", "PHP"),
        [".json"] = ("json", "JSON"),
        [".yml"] = ("yaml", "YAML"),
        [".yaml"] = ("yaml", "YAML"),
        [".toml"] = ("toml", "TOML"),
        [".ini"] = ("ini", "INI"),
        [".cfg"] = ("ini", "INI"),
        [".conf"] = ("conf", "Configuration"),
        [".cnf"] = ("ini", "INI"),
        [".env"] = ("env", "Environment"),
        [".properties"] = ("properties", "Properties"),
        [".service"] = ("ini", "systemd"),
        [".socket"] = ("ini", "systemd"),
        [".timer"] = ("ini", "systemd"),
        [".mount"] = ("ini", "systemd"),
        [".target"] = ("ini", "systemd"),
        [".path"] = ("ini", "systemd"),
        [".network"] = ("ini", "systemd"),
        [".netdev"] = ("ini", "systemd"),
        [".sql"] = ("sql", "SQL"),
        [".xml"] = ("xml", "XML"),
        [".html"] = ("html", "HTML"),
        [".plist"] = ("xml", "XML"),
        [".svg"] = ("xml", "XML"),
        [".ps1"] = ("powershell", "PowerShell"),
    };

    private static readonly Dictionary<string, (string Name, string Label)> s_byFileName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Dockerfile"] = ("dockerfile", "Dockerfile"),
        ["Containerfile"] = ("dockerfile", "Dockerfile"),
        ["sshd_config"] = ("sshconfig", "SSH"),
        ["ssh_config"] = ("sshconfig", "SSH"),
        ["haproxy.cfg"] = ("haproxy", "HAProxy"),
        ["crontab"] = ("sh", "crontab"),
        [".bashrc"] = ("sh", "Shell"),
        [".bash_profile"] = ("sh", "Shell"),
        [".profile"] = ("sh", "Shell"),
        [".zshrc"] = ("sh", "Shell"),
        [".zprofile"] = ("sh", "Shell"),
        [".env"] = ("env", "Environment"),
        ["docker-compose.yml"] = ("yaml", "YAML"),
    };

    // The name SyntaxHighlighter takes, and what to call the language on screen; null for plain text.
    public static (string Name, string Label)? Of(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var name = path[(path.LastIndexOf('/') + 1)..];
        if (s_byFileName.TryGetValue(name, out var known))
        {
            return known;
        }

        if (FromFolder(path) is { } folder)
        {
            return folder;
        }

        return s_byExtension.TryGetValue(Path.GetExtension(name), out var language) ? language : null;
    }

    private static (string Name, string Label)? FromFolder(string path) =>
        path.Contains("/nginx/", StringComparison.Ordinal) ? ("nginx", "nginx")
        : path.Contains("/apache2/", StringComparison.Ordinal) || path.Contains("/httpd/", StringComparison.Ordinal) ? ("apache", "Apache")
        : path.Contains("/.ssh/config", StringComparison.Ordinal) || path.Contains("/ssh/sshd_config.d/", StringComparison.Ordinal) ? ("sshconfig", "SSH")
        : path.Contains("/systemd/", StringComparison.Ordinal) && !Path.HasExtension(path) ? ("ini", "systemd")
        : path.Contains("/cron.d/", StringComparison.Ordinal) ? ("sh", "crontab")
        : null;
}
