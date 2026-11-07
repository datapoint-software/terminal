namespace Datapoint.Terminal.Shell.Areas;

// A window's layout is a tree: each area is a leaf, and each split puts two parts side by side or one above the other.
public abstract class AreaNode
{
    public abstract bool Contains(AreaKind kind);
}
