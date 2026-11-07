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

    // The approval mode each workspace last used (product decision 107); one without starts in Ask. Replaces
    // defaultApprovalMode, which is no longer read.
    public IReadOnlyList<WorkspaceMode> WorkspaceModes { get; init => field = value ?? []; } = [];

    public IReadOnlyList<string> RecentWorkspaces { get; init => field = value ?? []; } = [];

    public IReadOnlyList<string> OpenWorkspaces { get; init => field = value ?? []; } = [];

    public IReadOnlyList<WorkspaceTrustRecord> TrustedWorkspaces { get; init => field = value ?? []; } = [];

    public IReadOnlyList<ConfirmedConnections> ConfirmedConnections { get; init => field = value ?? []; } = [];

    // SHA-256 fingerprints of values the user marked as not secret, never the values themselves.
    public IReadOnlyList<string> NotSecrets { get; init => field = value ?? []; } = [];

    // Where the terminals docked beside the chat before areas could be moved; read only to start areaLayout from it.
    public Shell.TerminalSide TerminalSide { get; set; } = Shell.TerminalSide.Right;

    // Where the agent, terminals and documents sit, the same in every window (product decision 109), e.g.
    // "row(640;agent,column(280;terminals,documents))". Null until the user first moves or resizes an area.
    public string? AreaLayout { get; set; }

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

    // The window's colours: the system's, light, dark or the terminal theme's. Null in settings from before it could be
    // chosen, which read matchWindowToTheme instead.
    public Datapoint.Terminal.Themes.WindowTheme? WindowTheme { get; init; }

    // Written as well, so an older version still matches the window to the terminal theme when it's chosen.
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
