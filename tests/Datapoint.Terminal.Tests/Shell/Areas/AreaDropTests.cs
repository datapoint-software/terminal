using Avalonia;
using Datapoint.Terminal.Shell.Areas;

namespace Datapoint.Terminal.Tests.Shell.Areas;

public sealed class AreaDropTests
{
    private static readonly Rect s_window = new(0, 0, 1000, 600);

    private static readonly Dictionary<AreaKind, Rect> s_areas = new()
    {
        [AreaKind.Agent] = new Rect(0, 0, 400, 600),
        [AreaKind.Terminals] = new Rect(401, 0, 599, 300),
        [AreaKind.Hosts] = new Rect(401, 301, 599, 299),
    };

    [Fact]
    public void Docks_to_the_window_near_its_edge()
    {
        var drop = AreaDrop.Find(new Point(10, 300), s_window, s_areas, AreaKind.Hosts);

        Assert.Equal(new AreaDrop(null, AreaEdge.Left, new Rect(0, 0, 1000.0 / 3, 600)), drop);
    }

    [Theory]
    [InlineData(60, 300, AreaEdge.Left)]
    [InlineData(380, 300, AreaEdge.Right)]
    [InlineData(200, 40, AreaEdge.Top)]
    [InlineData(200, 560, AreaEdge.Bottom)]
    public void Docks_beside_the_area_under_the_pointer_on_its_nearest_edge(double x, double y, AreaEdge edge)
    {
        var drop = AreaDrop.Find(new Point(x, y), s_window, s_areas, AreaKind.Hosts);

        Assert.Equal(AreaKind.Agent, drop?.Target);
        Assert.Equal(edge, drop?.Edge);
    }

    [Fact]
    public void Previews_half_of_the_area_it_docks_beside()
    {
        var drop = AreaDrop.Find(new Point(380, 300), s_window, s_areas, AreaKind.Hosts);

        Assert.Equal(new Rect(200, 0, 200, 600), drop?.Preview);
    }

    [Fact]
    public void Offers_nothing_over_the_dragged_area_itself() =>
        Assert.Null(AreaDrop.Find(new Point(700, 450), s_window, s_areas, AreaKind.Hosts));
}
