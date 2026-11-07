using System.Text.Json.Serialization;
using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Agent.Workspaces;

namespace Datapoint.Terminal.Settings;

// Secrets are never saved here. Settings with a default other than zero or false are settable: the JSON source generator
// gives every init-only property a value, so one missing from a file an older version wrote would lose its default.
public sealed record AppSettings
{
    public const int CurrentVersion = 1;

    // Raised when a newer version of the application changes the format, so older versions don't overwrite it.
    public int Version { get; set; } = CurrentVersion;

    public IReadOnlyList<ProviderAccount> Providers { get; init => field = value ?? []; } = [];

    public string? SelectedProviderId { get; init; }

    public ApprovalMode DefaultApprovalMode { get; set; } = ApprovalMode.Ask;

    public IReadOnlyList<string> RecentWorkspaces { get; init => field = value ?? []; } = [];

    public IReadOnlyList<string> OpenWorkspaces { get; init => field = value ?? []; } = [];

    public IReadOnlyList<WorkspaceTrustRecord> TrustedWorkspaces { get; init => field = value ?? []; } = [];

    public IReadOnlyList<ConfirmedConnections> ConfirmedConnections { get; init => field = value ?? []; } = [];

    // SHA-256 fingerprints of values the user marked as not secret, never the values themselves.
    public IReadOnlyList<string> NotSecrets { get; init => field = value ?? []; } = [];

    // Where new windows dock the terminals beside the chat; each window can move its own. Replaces chatSide, which placed
    // the chat and is no longer read.
    public Shell.TerminalSide TerminalSide { get; set; } = Shell.TerminalSide.Right;

    // Desktop notifications when the agent finishes or needs the user in a background window.
    public bool Notifications { get; set; } = true;

    // Session recordings in each workspace's personal data, kept as long as its history (product decision 45).
    public bool RecordSessions { get; set; } = true;

    // In US dollars per window session; null for no limit.
    public decimal? SpendingLimit { get; init; }

    public Shell.TerminalTheme TerminalTheme { get; set; } = Shell.TerminalTheme.Dark;

    // Null uses the default list of monospace fonts.
    public string? TerminalFont { get; init; }

    public double TerminalFontSize { get; set; } = 14;

    // Themes the user imported or made; when one is chosen, it's used instead of TerminalTheme.
    public IReadOnlyList<Themes.ColorTheme> Themes { get; init => field = value ?? []; } = [];

    public string? CustomThemeId { get; init; }

    // The window's own colours follow the terminal theme's background and text.
    public bool MatchWindowToTheme { get; init; }

    // Overlays and cards appear without animating; the system's own reduce motion setting does the same.
    public bool ReduceMotion { get; init; }

    // The agent searches the web and reads pages without asking each time (product decision 105).
    public bool AlwaysAllowWeb { get; init; }

    public WebSearchService WebSearch { get; init; } = WebSearchService.Provider;

    // The SearXNG server's address, when that's the search service.
    public string? SearxngServer { get; init; }

    // How long conversation history is kept, in days; null keeps it forever (product decision 52).
    public int? HistoryDays { get; init; }

    [JsonIgnore]
    public ProviderAccount? SelectedProvider =>
        Providers.FirstOrDefault(provider => string.Equals(provider.Id, SelectedProviderId, StringComparison.Ordinal)) ?? (Providers.Count > 0 ? Providers[0] : null);
}
