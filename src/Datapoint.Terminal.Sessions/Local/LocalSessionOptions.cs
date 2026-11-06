namespace Datapoint.Terminal.Sessions.Local;

public sealed record LocalSessionOptions
{
    public string? Shell { get; init; }

    // A login shell, as terminals on macOS start.
    public bool Login { get; init; } = OperatingSystem.IsMacOS();

    public string WorkingDirectory { get; init; } = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);

    public int Columns { get; init; } = 80;

    public int Rows { get; init; } = 24;

    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public bool ShellIntegration { get; init; } = true;
}
