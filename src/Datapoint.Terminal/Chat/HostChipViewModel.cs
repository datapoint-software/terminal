using Datapoint.Terminal.Agent;

namespace Datapoint.Terminal.Chat;

// A host named on a look request or a plan step, with its environment so production stands out wherever it's named.
public sealed class HostChipViewModel(string name, string? environment)
{
    public string Name => name;

    public string? Environment => environment;

    public bool HasEnvironment => environment is not null;

    // Set in capitals, small, so it reads as a label beside the name.
    public string? EnvironmentLabel => environment?.ToUpper(System.Globalization.CultureInfo.CurrentCulture);

    public bool IsProduction => EnvironmentLabels.IsProduction(environment);

    public string AccessibleName => environment is null ? name : $"{name}, {environment}";

    public static IReadOnlyList<HostChipViewModel> For(IEnumerable<string> names, IReadOnlyDictionary<string, string> environments) =>
        [.. names.Select(name => new HostChipViewModel(name, environments.GetValueOrDefault(name)))];
}
