using System.Text.Json.Serialization;
using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Agent.Projects;

namespace Datapoint.Terminal.Settings;

// Secrets are never saved here.
public sealed record AppSettings
{
    public const int CurrentVersion = 1;

    // Raised when a newer version of the application changes the format, so older versions don't overwrite it.
    public int Version { get; init; } = CurrentVersion;

    public IReadOnlyList<ProviderAccount> Providers { get; init => field = value ?? []; } = [];

    public string? SelectedProviderId { get; init; }

    public ApprovalMode DefaultApprovalMode { get; init; } = ApprovalMode.Ask;

    public IReadOnlyList<string> RecentProjects { get; init => field = value ?? []; } = [];

    public IReadOnlyList<string> OpenProjects { get; init => field = value ?? []; } = [];

    public IReadOnlyList<ProjectTrustRecord> TrustedProjects { get; init => field = value ?? []; } = [];

    // Projects whose owner chose not to add .terminal/ to .gitignore, so they aren't asked again.
    public IReadOnlyList<string> DeclinedGitIgnore { get; init => field = value ?? []; } = [];

    public IReadOnlyList<ConfirmedConnections> ConfirmedConnections { get; init => field = value ?? []; } = [];

    // Projects already warned that they're inside a sync service's folder, so the warning is shown once.
    public IReadOnlyList<string> WarnedSyncFolders { get; init => field = value ?? []; } = [];

    // SHA-256 fingerprints of values the user marked as not secret, never the values themselves.
    public IReadOnlyList<string> NotSecrets { get; init => field = value ?? []; } = [];

    // Where new windows put the chat pane; each window can move its own.
    public Shell.ChatSide ChatSide { get; init; } = Shell.ChatSide.Right;

    public Shell.TerminalTheme TerminalTheme { get; init; } = Shell.TerminalTheme.Dark;

    // Null uses the default list of monospace fonts.
    public string? TerminalFont { get; init; }

    public double TerminalFontSize { get; init; } = 14;

    // How long conversation history is kept, in days; null keeps it forever (product decision 52).
    public int? HistoryDays { get; init; }

    [JsonIgnore]
    public ProviderAccount? SelectedProvider =>
        Providers.FirstOrDefault(provider => string.Equals(provider.Id, SelectedProviderId, StringComparison.Ordinal)) ?? (Providers.Count > 0 ? Providers[0] : null);
}
