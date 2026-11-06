namespace Datapoint.Terminal.Chat;

/// <summary>A provider and model the user can pick in the chat.</summary>
public sealed record ModelChoice(string ProviderId, string Model, string Label)
{
    public override string ToString() => Label;
}
