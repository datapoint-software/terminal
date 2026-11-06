namespace Datapoint.Terminal.Chat;

/// <summary>A provider and model the user can pick in the chat.</summary>
/// <param name="Name">The model's short name, e.g. "Opus 5.5".</param>
/// <param name="Provider">The provider's name, shown in the menu when there's more than one provider; otherwise empty.</param>
public sealed record ModelChoice(string ProviderId, string Model, string Name, string Provider)
{
    public bool HasProvider => Provider.Length > 0;

    public override string ToString() => HasProvider ? $"{Name}, {Provider}" : Name;
}
