namespace Datapoint.Terminal.Shell;

// Tabs shown together, in one row or one column. Each pane is still a tab of its own, with its own name in the tab strip and
// for the agent, so splitting changes what the user sees and nothing about how the agent addresses terminals.
public sealed class PaneGroup(bool isBeside)
{
    public const int MaxPanes = 4;

    public bool IsBeside { get; set; } = isBeside;

    private readonly List<TerminalTab> _tabs = [];

    public IReadOnlyList<TerminalTab> Tabs => _tabs;

    public IReadOnlyList<double>? Sizes { get; set; }

    public int IndexOf(TerminalTab tab) => _tabs.IndexOf(tab);

    public bool IsFull => Tabs.Count >= MaxPanes;

    public void Insert(TerminalTab after, TerminalTab tab)
    {
        if (Tabs.Count == 0)
        {
            _tabs.Add(after);
            after.Group = this;
        }

        _tabs.Insert(_tabs.IndexOf(after) + 1, tab);
        tab.Group = this;
        Sizes = null;
    }

    // A group left with one pane ends, so that tab is shown alone again.
    public static void Leave(TerminalTab tab)
    {
        if (tab.Group is not { } group)
        {
            return;
        }

        group._tabs.Remove(tab);
        tab.Group = null;
        group.Sizes = null;
        if (group.Tabs.Count != 1)
        {
            return;
        }

        group._tabs[0].Group = null;
        group._tabs.Clear();
    }
}
