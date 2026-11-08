using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Chat;

// An approval mode, named in the interface's language (product decision 150).
public sealed class ModeOption(ApprovalMode mode, Func<string> label, Func<string> help) : LocalizedObject
{
    public ApprovalMode Mode { get; } = mode;

    public string Label => label();

    public string Help => help();

    public override string ToString() => Label;
}
