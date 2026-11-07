using Datapoint.Terminal.Shell;
using Datapoint.Terminal.Shell.Areas;

namespace Datapoint.Terminal.Tests.Shell.Areas;

public sealed class AreaLayoutTests
{
    private static bool All(AreaKind kind) => true;

    [Theory]
    [InlineData(TerminalSide.Right, "row(640;agent,column(280;terminals,documents))")]
    [InlineData(TerminalSide.Left, "row(640;column(280;terminals,documents),agent)")]
    [InlineData(TerminalSide.Bottom, "column(300;agent,row(420;terminals,documents))")]
    public void Starts_from_the_side_the_terminals_were_on(TerminalSide side, string expected) =>
        Assert.Equal(expected, AreaLayout.Format(AreaLayout.FromSide(side)));

    [Theory]
    [InlineData(AreaKind.Agent, AreaKind.Documents, AreaEdge.Right, "column(280;terminals,row(420;documents,agent))")]
    [InlineData(AreaKind.Documents, AreaKind.Agent, AreaEdge.Left, "row(640;row(420;documents,agent),terminals)")]
    [InlineData(AreaKind.Terminals, AreaKind.Agent, AreaEdge.Bottom, "row(640;column(300;agent,terminals),documents)")]
    public void Moves_an_area_beside_another(AreaKind moved, AreaKind target, AreaEdge edge, string expected) =>
        Assert.Equal(expected, AreaLayout.Format(AreaLayout.Move(AreaLayout.Default(), moved, target, edge)));

    [Theory]
    [InlineData(AreaEdge.Left, "row(420;documents,row(640;agent,terminals))")]
    [InlineData(AreaEdge.Bottom, "column(280;row(640;agent,terminals),documents)")]
    public void Moves_an_area_to_an_edge_of_the_window(AreaEdge edge, string expected) =>
        Assert.Equal(expected, AreaLayout.Format(AreaLayout.Move(AreaLayout.Default(), AreaKind.Documents, null, edge)));

    [Fact]
    public void Leaves_the_layout_as_it_is_when_an_area_is_dropped_on_itself()
    {
        var layout = AreaLayout.Default();

        Assert.Same(layout, AreaLayout.Move(layout, AreaKind.Terminals, AreaKind.Terminals, AreaEdge.Left));
    }

    [Fact]
    public void Reads_back_what_it_saves()
    {
        const string text = "column(312;row(420;documents,agent),terminals)";

        Assert.Equal(text, AreaLayout.Format(AreaLayout.Parse(text)!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("agent")]
    [InlineData("row(640;agent,terminals)")]
    [InlineData("row(640;agent,column(280;terminals,terminals))")]
    [InlineData("row(640;agent,column(280;terminals,documents)")]
    [InlineData("row(640;agent,column(280;terminals,documents)))")]
    [InlineData("row(-5;agent,column(280;terminals,documents))")]
    [InlineData("grid(640;agent,column(280;terminals,documents))")]
    [InlineData("row(640;agent,column(280;terminals,sidebar))")]
    public void Refuses_text_that_is_not_a_layout_of_every_area_once(string text) =>
        Assert.Null(AreaLayout.Parse(text));

    [Fact]
    public void Needs_room_for_each_shown_area_and_the_lines_between_them()
    {
        var layout = AreaLayout.Default();

        Assert.Equal(320 + 1 + 200, AreaLayout.MinimumLength(layout, isRow: true, All));
        Assert.Equal(120 + 1 + 120, AreaLayout.MinimumLength(layout, isRow: false, All));
        Assert.Equal(120, AreaLayout.MinimumLength(layout, isRow: false, kind => kind != AreaKind.Documents));
    }

    [Fact]
    public void Shrinks_a_fixed_part_that_would_leave_the_rest_too_little_room()
    {
        var layout = (AreaSplit)AreaLayout.Default();

        AreaLayout.Fit(layout, 800, 600, All);

        Assert.Equal(800 - 320 - 1, layout.Size);
    }

    [Fact]
    public void Keeps_the_size_of_the_part_without_the_agent()
    {
        var layout = (AreaSplit)AreaLayout.Default();

        Assert.True(layout.Fixed.Contains(AreaKind.Terminals));
        Assert.False(layout.Fixed.Contains(AreaKind.Agent));
    }
}
