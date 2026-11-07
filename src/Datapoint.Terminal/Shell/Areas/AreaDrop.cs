using Avalonia;

namespace Datapoint.Terminal.Shell.Areas;

// Where a dragged area would land: on an edge of another area, or of the whole window (no target), and the part of the window
// shown highlighted until it's dropped.
public sealed record AreaDrop(AreaKind? Target, AreaEdge Edge, Rect Preview)
{
    // How close to the window's own edge the pointer has to be to dock there rather than beside the area under it.
    public const double WindowEdgeWidth = 24;

    public static AreaDrop? Find(Point point, Rect window, IReadOnlyDictionary<AreaKind, Rect> areas, AreaKind dragged)
    {
        ArgumentNullException.ThrowIfNull(areas);
        if (!window.Contains(point))
        {
            return null;
        }

        if (WindowEdgeAt(point, window) is { } edge)
        {
            return new AreaDrop(null, edge, Part(window, edge, 1.0 / 3));
        }

        foreach (var (kind, area) in areas)
        {
            if (kind == dragged || !area.Contains(point))
            {
                continue;
            }

            var side = NearestEdge(point, area);
            return new AreaDrop(kind, side, Part(area, side, 0.5));
        }

        return null;
    }

    private static AreaEdge? WindowEdgeAt(Point point, Rect window)
    {
        var distances = new[]
        {
            (Edge: AreaEdge.Left, Distance: point.X - window.Left),
            (Edge: AreaEdge.Right, Distance: window.Right - point.X),
            (Edge: AreaEdge.Top, Distance: point.Y - window.Top),
            (Edge: AreaEdge.Bottom, Distance: window.Bottom - point.Y),
        };
        var nearest = distances.MinBy(item => item.Distance);
        return nearest.Distance <= WindowEdgeWidth ? nearest.Edge : null;
    }

    // The edge whose triangle, from the area's centre, holds the point.
    private static AreaEdge NearestEdge(Point point, Rect area)
    {
        var x = (point.X - area.Center.X) / Math.Max(1, area.Width);
        var y = (point.Y - area.Center.Y) / Math.Max(1, area.Height);
        if (Math.Abs(x) >= Math.Abs(y))
        {
            return x < 0 ? AreaEdge.Left : AreaEdge.Right;
        }

        return y < 0 ? AreaEdge.Top : AreaEdge.Bottom;
    }

    private static Rect Part(Rect area, AreaEdge edge, double share) => edge switch
    {
        AreaEdge.Left => new Rect(area.X, area.Y, area.Width * share, area.Height),
        AreaEdge.Right => new Rect(area.Right - (area.Width * share), area.Y, area.Width * share, area.Height),
        AreaEdge.Top => new Rect(area.X, area.Y, area.Width, area.Height * share),
        _ => new Rect(area.X, area.Bottom - (area.Height * share), area.Width, area.Height * share),
    };
}
