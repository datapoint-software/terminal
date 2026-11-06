using Datapoint.Terminal.Agent.Providers;

namespace Datapoint.Terminal.Settings;

public sealed record KindOption(ProviderKind Kind, string Label)
{
    public override string ToString() => Label;
}
