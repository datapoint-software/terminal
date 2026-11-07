namespace Datapoint.Terminal.Shell.Areas;

public sealed class AreaLeaf(AreaKind kind) : AreaNode
{
    public AreaKind Kind { get; } = kind;

    public override bool Contains(AreaKind kind) => Kind == kind;
}
