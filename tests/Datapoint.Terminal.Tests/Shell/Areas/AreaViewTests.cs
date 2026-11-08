using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Datapoint.Terminal.Shell.Areas;

namespace Datapoint.Terminal.Tests.Shell.Areas;

public sealed class AreaViewTests
{
    private readonly Dictionary<AreaKind, Control> _areas = new()
    {
        [AreaKind.Agent] = new Border(),
        [AreaKind.Terminals] = new Border(),
        [AreaKind.Hosts] = new Border(),
        [AreaKind.Skills] = new Border(),
        [AreaKind.Workspace] = new Border(),
        [AreaKind.ServerFiles] = new Border(),
        [AreaKind.Plan] = new Border(),
    };

    // A host's page open, and nothing else.
    private static bool NoPlan(AreaKind kind) => kind is AreaKind.Agent or AreaKind.Terminals or AreaKind.Hosts;

    private static bool Usual(AreaKind kind) => kind is AreaKind.Agent or AreaKind.Terminals;

    private (Window Window, AreaView View) Show(AreaNode layout, Func<AreaKind, bool> isShown, AreaKind? collapsed = null, double width = 1000)
    {
        var host = new Panel();
        var view = new AreaView(host, _areas);
        view.Show(layout, isShown, collapsed);
        var window = new Window { Width = width, Height = 700, Content = host };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, view);
    }

    [AvaloniaFact]
    public void Gives_the_agent_what_the_fixed_parts_leave()
    {
        var (window, _) = Show(AreaLayout.Default(), NoPlan);

        Assert.Equal(new Avalonia.Rect(0, 0, 359, 700), _areas[AreaKind.Agent].Bounds);
        Assert.Equal(new Avalonia.Rect(0, 0, 640, 419), _areas[AreaKind.Terminals].Bounds);
        Assert.Equal(280, _areas[AreaKind.Hosts].Bounds.Height);
        window.Close();
    }

    [AvaloniaFact]
    public void Gives_a_hidden_area_no_space()
    {
        var (window, _) = Show(AreaLayout.Default(), Usual);

        Assert.False(_areas[AreaKind.Hosts].IsEffectivelyVisible);
        Assert.Equal(700, _areas[AreaKind.Terminals].Bounds.Height);
        window.Close();
    }

    [AvaloniaFact]
    public void Moves_the_areas_when_the_layout_changes()
    {
        var (window, view) = Show(AreaLayout.Default(), NoPlan);

        view.Show(AreaLayout.FromSide(Datapoint.Terminal.Shell.TerminalSide.Left), NoPlan);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(641, _areas[AreaKind.Agent].TranslatePoint(default, window)?.X);
        window.Close();
    }

    [AvaloniaFact]
    public void Gives_a_collapsed_area_only_the_size_it_asks_for_beside_the_agent()
    {
        _areas[AreaKind.Terminals] = new Border { Width = 40 };
        var (window, view) = Show(AreaLayout.Default(), Usual, AreaKind.Terminals);

        Assert.True(view.IsCollapsedBeside);
        Assert.Equal(959, _areas[AreaKind.Agent].Bounds.Width);
        Assert.Equal(960, _areas[AreaKind.Terminals].TranslatePoint(default, window)?.X);
        Assert.Empty(window.GetVisualDescendants().OfType<GridSplitter>());
        window.Close();
    }

    [AvaloniaFact]
    public void Lays_a_collapsed_area_across_the_one_below_it()
    {
        _areas[AreaKind.Terminals] = new Border { Height = 36 };
        var (window, view) = Show(AreaLayout.Default(), NoPlan, AreaKind.Terminals);

        Assert.False(view.IsCollapsedBeside);
        Assert.Equal(36, _areas[AreaKind.Terminals].Bounds.Height);
        Assert.Equal(663, _areas[AreaKind.Hosts].Bounds.Height);
        Assert.Equal(359, _areas[AreaKind.Agent].Bounds.Width);
        window.Close();
    }

    [AvaloniaFact]
    public void Moves_only_the_divider_when_only_sizes_change()
    {
        var (window, view) = Show(AreaLayout.Default(), NoPlan);
        var parent = _areas[AreaKind.Terminals].Parent;
        var narrower = AreaLayout.Default();
        ((AreaSplit)narrower).Size = 500;

        view.Show(narrower, NoPlan);
        Dispatcher.UIThread.RunJobs();

        Assert.Same(parent, _areas[AreaKind.Terminals].Parent);
        Assert.Equal(500, _areas[AreaKind.Terminals].Bounds.Width);
        Assert.Equal(500, ((AreaSplit)view.Shown!).Size);
        window.Close();
    }

    [AvaloniaFact]
    public void Opens_the_plan_beside_the_agent()
    {
        var layout = AreaLayout.Default();
        AreaLayout.Fit(layout, 1300, 700, kind => kind is AreaKind.Agent or AreaKind.Terminals or AreaKind.Plan);
        var (window, _) = Show(layout, kind => kind is AreaKind.Agent or AreaKind.Terminals or AreaKind.Plan, width: 1300);

        Assert.Equal(new Avalonia.Rect(0, 0, 320, 700), _areas[AreaKind.Agent].Bounds);
        Assert.Equal(new Avalonia.Rect(321, 0, 480, 700), _areas[AreaKind.Plan].Bounds);
        Assert.Equal(802, _areas[AreaKind.Terminals].TranslatePoint(default, window)?.X);
        window.Close();
    }
}
