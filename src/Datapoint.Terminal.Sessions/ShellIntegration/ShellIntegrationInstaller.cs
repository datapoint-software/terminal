namespace Datapoint.Terminal.Sessions.ShellIntegration;

// Never changes the user's files: bash's --init-file, zsh's ZDOTDIR, fish's --init-command and PowerShell's -Command load our scripts instead.
public sealed class ShellIntegrationInstaller : IDisposable
{
    public const string NonceVariable = "DATAPOINT_NONCE";

    private ShellIntegrationInstaller(string folder) => Folder = folder;

    // Readable only by the current user.
    public string Folder { get; }

    public static ShellIntegrationInstaller Create()
    {
        var folder = Path.Combine(Path.GetTempPath(), "datapoint-terminal-" + Convert.ToHexStringLower(Security.Cryptography.RandomBytes.Create(8)));
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(folder);
        }
        else
        {
            Directory.CreateDirectory(folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        var installer = new ShellIntegrationInstaller(folder);
        installer.WriteScripts();
        return installer;
    }

    public static ShellKind KindOf(string program)
    {
        var fileName = program[(program.LastIndexOfAny(['/', '\\']) + 1)..];
        var name = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
        return name switch
        {
            "bash" => ShellKind.Bash,
            "zsh" => ShellKind.Zsh,
            "fish" => ShellKind.Fish,
            "pwsh" or "powershell" => ShellKind.PowerShell,
            _ => ShellKind.Other,
        };
    }

    public ShellLaunch Prepare(string program, bool login, string nonce)
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal) { [NonceVariable] = nonce };
        var kind = KindOf(program);
        IReadOnlyList<string> arguments = kind switch
        {
            ShellKind.Bash => PrepareBash(login, environment),
            ShellKind.Zsh => PrepareZsh(login, environment),
            ShellKind.Fish => [.. LoginArgument(login), "--init-command", $"source {QuoteForFish(Path.Combine(Folder, "fish.fish"))}"],
            ShellKind.PowerShell => ["-NoLogo", "-NoExit", "-Command", $". {QuoteForPowerShell(Path.Combine(Folder, "powershell.ps1"))}"],
            _ => OperatingSystem.IsWindows() ? [] : LoginArgument(login),
        };
        return new ShellLaunch(program, arguments, environment, kind);
    }

    private string[] PrepareBash(bool login, Dictionary<string, string> environment)
    {
        if (login)
        {
            environment["DATAPOINT_LOGIN"] = "1";
        }

        return ["--init-file", Path.Combine(Folder, "bash.sh")];
    }

    private string[] PrepareZsh(bool login, Dictionary<string, string> environment)
    {
        environment["DATAPOINT_USER_ZDOTDIR"] = Environment.GetEnvironmentVariable("ZDOTDIR") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        environment["ZDOTDIR"] = Path.Combine(Folder, "zsh");
        return LoginArgument(login);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Folder, recursive: true);
        }
        catch (IOException)
        {
            // A shell still has a script open (Windows); the temporary folder is cleaned up by the system.
        }
        catch (UnauthorizedAccessException)
        {
            // As above.
        }
    }

    private static string[] LoginArgument(bool login) => login ? ["-l"] : [];

    private static string QuoteForFish(string path) => "'" + path.Replace(@"\", @"\\", StringComparison.Ordinal).Replace("'", @"\'", StringComparison.Ordinal) + "'";

    private static string QuoteForPowerShell(string path) => "'" + path.Replace("'", "''", StringComparison.Ordinal) + "'";

    private void WriteScripts()
    {
        const string prefix = "ShellIntegration/";
        var assembly = typeof(ShellIntegrationInstaller).Assembly;
        foreach (var name in assembly.GetManifestResourceNames().Where(name => name.StartsWith(prefix, StringComparison.Ordinal)))
        {
            var relative = name[prefix.Length..].Replace('\\', '/');
            var path = Path.Combine(Folder, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var source = assembly.GetManifestResourceStream(name)!;
            using var target = File.Create(path);
            source.CopyTo(target);
        }
    }
}
