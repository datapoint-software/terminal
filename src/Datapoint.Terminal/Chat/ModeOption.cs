using Datapoint.Terminal.Agent;

namespace Datapoint.Terminal.Chat;

public sealed record ModeOption(ApprovalMode Mode, string Label, string Help)
{
    public override string ToString() => Label;
}
