using Avalonia.Headless.XUnit;
using Datapoint.Terminal.Controls;
using Datapoint.Terminal.Shell;

namespace Datapoint.Terminal.Tests.Shell;

public sealed class PaneGroupTests
{
    private static TerminalTab Tab(string name) => new(name, new TerminalView(), _ => { });

    [AvaloniaFact]
    public void Inserts_a_pane_after_the_one_split()
    {
        var (a, b, c) = (Tab("A"), Tab("B"), Tab("C"));
        var group = new PaneGroup(isBeside: true);

        group.Insert(a, b);
        group.Insert(a, c);

        Assert.Equal([a, c, b], group.Tabs);
        Assert.All(group.Tabs, tab => Assert.Same(group, tab.Group));
    }

    [AvaloniaFact]
    public void Ends_the_group_when_one_pane_is_left()
    {
        var (a, b) = (Tab("A"), Tab("B"));
        var group = new PaneGroup(isBeside: false);
        group.Insert(a, b);

        PaneGroup.Leave(b);

        Assert.Null(a.Group);
        Assert.Null(b.Group);
        Assert.Empty(group.Tabs);
    }

    [AvaloniaFact]
    public void Keeps_the_group_while_two_panes_remain_and_is_full_at_four()
    {
        var tabs = new[] { Tab("A"), Tab("B"), Tab("C"), Tab("D") };
        var group = new PaneGroup(isBeside: true) { Sizes = [1, 2] };
        group.Insert(tabs[0], tabs[1]);
        group.Insert(tabs[1], tabs[2]);
        group.Insert(tabs[2], tabs[3]);
        Assert.True(group.IsFull);

        PaneGroup.Leave(tabs[1]);

        Assert.Equal([tabs[0], tabs[2], tabs[3]], group.Tabs);
        Assert.False(group.IsFull);
        Assert.Null(group.Sizes);
    }
}
