namespace Datapoint.Terminal.Shell.Areas;

// Two parts side by side (a row) or one above the other. The part without the agent keeps its size in pixels as the window
// grows or shrinks, so terminals keep their columns and the chat takes the rest; when neither part has it, the second does.
public sealed class AreaSplit(bool isRow, AreaNode first, AreaNode second, double size) : AreaNode
{
    public bool IsRow { get; } = isRow;

    public AreaNode First { get; } = first;

    public AreaNode Second { get; } = second;

    // The fixed part's width or height, changed as the user drags the divider.
    public double Size { get; set; } = size;

    public bool FixesFirst => Second.Contains(AreaKind.Agent);

    public AreaNode Fixed => FixesFirst ? First : Second;

    public override bool Contains(AreaKind kind) => First.Contains(kind) || Second.Contains(kind);
}
