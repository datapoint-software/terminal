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

    [JsonIgnore]
    public ProviderAccount? SelectedProvider =>
        Providers.FirstOrDefault(provider => string.Equals(provider.Id, SelectedProviderId, StringComparison.Ordinal)) ?? (Providers.Count > 0 ? Providers[0] : null);
}
