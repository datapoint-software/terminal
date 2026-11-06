namespace Datapoint.Terminal.Sessions.Local;

public static class DefaultShell
{
    /// <summary>The user's shell: $SHELL on macOS and Linux, PowerShell 7 or Windows PowerShell on Windows.</summary>
    public static string Find()
    {
        if (!OperatingSystem.IsWindows())
        {
            var shell = Environment.GetEnvironmentVariable("SHELL");
            return !string.IsNullOrEmpty(shell) && File.Exists(shell) ? shell : "/bin/sh";
        }

        var pwsh = FindOnPath("pwsh.exe");
        return pwsh ?? Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
    }

    private static string? FindOnPath(string program)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(folder => Path.Combine(folder, program))
            .FirstOrDefault(File.Exists);
    }
}
