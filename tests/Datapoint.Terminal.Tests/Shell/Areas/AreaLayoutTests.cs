using Datapoint.Terminal.Shell;
using Datapoint.Terminal.Shell.Areas;

namespace Datapoint.Terminal.Tests.Shell.Areas;

public sealed class AreaLayoutTests
{
    private static bool All(AreaKind kind) => true;

    private static bool NoPlan(AreaKind kind) => kind is AreaKind.Agent or AreaKind.Terminals or AreaKind.Hosts;

    private static bool WithPlan(AreaKind kind) => kind != AreaKind.Hosts && (NoPlan(kind) || kind == AreaKind.Plan);

    // Each kind of document, together where a single documents area used to be.
    private const string Documents = "row(420;hosts,row(420;skills,row(420;workspace,serverfiles)))";
    private const string OtherDocuments = "row(420;skills,row(420;workspace,serverfiles))";

    [Theory]
    [InlineData(TerminalSide.Right, "row(640;row(680;agent,plan),column(280;terminals," + Documents + "))")]
    [InlineData(TerminalSide.Left, "row(640;column(280;terminals," + Documents + "),row(680;agent,plan))")]
    [InlineData(TerminalSide.Bottom, "column(300;row(680;agent,plan),row(420;terminals,column(280;hosts,column(280;skills,column(280;workspace,serverfiles)))))")]
    public void Starts_from_the_side_the_terminals_were_on(TerminalSide side, string expected) =>
        Assert.Equal(expected, AreaLayout.Format(AreaLayout.FromSide(side)));

    [Theory]
    [InlineData(AreaKind.Hosts, AreaKind.Agent, AreaEdge.Left, "row(640;row(680;row(420;hosts,agent),plan),column(280;terminals," + OtherDocuments + "))")]
    [InlineData(AreaKind.Terminals, AreaKind.Agent, AreaEdge.Bottom, "row(640;row(680;column(300;agent,terminals),plan)," + Documents + ")")]
    [InlineData(AreaKind.Plan, AreaKind.Skills, AreaEdge.Bottom, "row(640;agent,column(280;terminals,row(420;hosts,row(420;column(360;skills,plan),row(420;workspace,serverfiles)))))")]
    public void Moves_an_area_beside_another(AreaKind moved, AreaKind target, AreaEdge edge, string expected) =>
        Assert.Equal(expected, AreaLayout.Format(AreaLayout.Move(AreaLayout.Default(), moved, target, edge)));

    [Theory]
    [InlineData(AreaEdge.Left, "row(420;hosts,row(640;row(680;agent,plan),column(280;terminals," + OtherDocuments + ")))")]
    [InlineData(AreaEdge.Bottom, "column(280;row(640;row(680;agent,plan),column(280;terminals," + OtherDocuments + ")),hosts)")]
    public void Moves_an_area_to_an_edge_of_the_window(AreaEdge edge, string expected) =>
        Assert.Equal(expected, AreaLayout.Format(AreaLayout.Move(AreaLayout.Default(), AreaKind.Hosts, null, edge)));

    [Fact]
    public void Leaves_the_layout_as_it_is_when_an_area_is_dropped_on_itself()
    {
        var layout = AreaLayout.Default();

        Assert.Same(layout, AreaLayout.Move(layout, AreaKind.Terminals, AreaKind.Terminals, AreaEdge.Left));
    }

    [Fact]
    public void Reads_back_what_it_saves()
    {
        const string text = "column(312;row(680;" + Documents + ",plan),row(500;agent,terminals))";

        Assert.Equal(text, AreaLayout.Format(AreaLayout.Parse(text)!));
    }

    [Fact]
    public void Reads_a_layout_saved_before_the_plan_and_each_kind_of_document_had_their_own_areas() =>
        Assert.Equal("column(312;row(420;" + Documents + ",row(680;agent,plan)),terminals)", AreaLayout.Format(AreaLayout.Parse("column(312;row(420;documents,agent),terminals)")!));

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
    [InlineData("row(640;row(680;agent,plan),column(280;plan,documents))")]
    public void Refuses_text_that_is_not_a_layout_of_every_area_once(string text) =>
        Assert.Null(AreaLayout.Parse(text));

    [Fact]
    public void Needs_room_for_each_shown_area_and_the_lines_between_them()
    {
        var layout = AreaLayout.Default();

        Assert.Equal(320 + 1 + 200, AreaLayout.MinimumLength(layout, isRow: true, NoPlan));
        Assert.Equal(320 + 1 + 480 + 1 + 200, AreaLayout.MinimumLength(layout, isRow: true, WithPlan));
        Assert.Equal(320 + 1 + 480 + 1 + (4 * 200) + 3, AreaLayout.MinimumLength(layout, isRow: true, All));
        Assert.Equal(120 + 1 + 120, AreaLayout.MinimumLength(layout, isRow: false, All));
        Assert.Equal(120, AreaLayout.MinimumLength(layout, isRow: false, kind => kind is AreaKind.Agent or AreaKind.Terminals));
    }

    [Fact]
    public void Shrinks_a_fixed_part_that_would_leave_the_rest_too_little_room()
    {
        var layout = (AreaSplit)AreaLayout.Default();

        AreaLayout.Fit(layout, 800, 600, NoPlan);

        Assert.Equal(800 - 320 - 1, layout.Size);
    }

    [Fact]
    public void Leaves_an_open_plan_room_to_be_read()
    {
        var layout = (AreaSplit)AreaLayout.Default();

        AreaLayout.Fit(layout, 1100, 600, WithPlan);

        Assert.Equal(1100 - 320 - 1 - 480 - 1, layout.Size);
        Assert.Equal(480, ((AreaSplit)layout.First).Size);
    }

    [Fact]
    public void Keeps_the_size_of_the_part_without_the_agent()
    {
        var layout = (AreaSplit)AreaLayout.Default();

        Assert.True(layout.Fixed.Contains(AreaKind.Terminals));
        Assert.False(layout.Fixed.Contains(AreaKind.Agent));
    }
}
