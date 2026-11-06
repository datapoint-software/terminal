namespace Datapoint.Terminal.Sessions.Local;

public sealed record LocalSessionOptions
{
    /// <summary>The shell to start; the user's default shell when null.</summary>
    public string? Shell { get; init; }

    /// <summary>Starts the shell as a login shell, as terminals on macOS do.</summary>
    public bool Login { get; init; } = OperatingSystem.IsMacOS();

    public string WorkingDirectory { get; init; } = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);

    public int Columns { get; init; } = 80;

    public int Rows { get; init; } = 24;

    /// <summary>Variables to set (or, with an empty value, remove) on top of the application's environment.</summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Loads our shell integration scripts; when false the shell starts exactly as the user configured it.</summary>
    public bool ShellIntegration { get; init; } = true;
}
