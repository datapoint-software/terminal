namespace Datapoint.Terminal.Chat;

public sealed record ModelChoice(string ProviderId, string Model, string Name, string Provider)
{
    public bool HasProvider => Provider.Length > 0;

    public override string ToString() => HasProvider ? $"{Name}, {Provider}" : Name;
}
