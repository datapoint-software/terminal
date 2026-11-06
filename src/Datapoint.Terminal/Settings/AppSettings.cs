using System.Text.Json.Serialization;
using Datapoint.Terminal.Agent;

namespace Datapoint.Terminal.Settings;

/// <summary>Everything the application remembers between runs, apart from secrets. Saved as JSON.</summary>
public sealed record AppSettings
{
    public const int CurrentVersion = 1;

    /// <summary>Raised when a newer version of the application changes the format, so older versions don't overwrite it.</summary>
    public int Version { get; init; } = CurrentVersion;

    public IReadOnlyList<ProviderAccount> Providers { get; init; } = [];

    /// <summary>The provider the agent uses, by <see cref="ProviderAccount.Id"/>.</summary>
    public string? SelectedProviderId { get; init; }

    public ApprovalMode DefaultApprovalMode { get; init; } = ApprovalMode.Ask;

    /// <summary>Worked out from <see cref="SelectedProviderId"/>, so it isn't saved.</summary>
    [JsonIgnore]
    public ProviderAccount? SelectedProvider =>
        Providers.FirstOrDefault(provider => string.Equals(provider.Id, SelectedProviderId, StringComparison.Ordinal)) ?? (Providers.Count > 0 ? Providers[0] : null);
}
