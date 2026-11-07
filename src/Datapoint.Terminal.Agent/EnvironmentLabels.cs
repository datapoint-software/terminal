namespace Datapoint.Terminal.Agent;

public static class EnvironmentLabels
{
    // "production", "prod", "prod-eu" and the like.
    public static bool IsProduction(string? environment) => environment?.StartsWith("prod", StringComparison.OrdinalIgnoreCase) == true;
}
