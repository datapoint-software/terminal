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
        [AreaKind.Documents] = new Border(),
    };

    private (Window Window, AreaView View) Show(AreaNode layout, Func<AreaKind, bool> isShown, AreaKind? collapsed = null)
    {
        var host = new Panel();
        var view = new AreaView(host, _areas);
        view.Show(layout, isShown, collapsed);
        var window = new Window { Width = 1000, Height = 700, Content = host };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, view);
    }

    [AvaloniaFact]
    public void Gives_the_agent_what_the_fixed_parts_leave()
    {
        var (window, _) = Show(AreaLayout.Default(), _ => true);

        Assert.Equal(new Avalonia.Rect(0, 0, 359, 700), _areas[AreaKind.Agent].Bounds);
        Assert.Equal(new Avalonia.Rect(0, 0, 640, 419), _areas[AreaKind.Terminals].Bounds);
        Assert.Equal(280, _areas[AreaKind.Documents].Bounds.Height);
        window.Close();
    }

    [AvaloniaFact]
    public void Gives_a_hidden_area_no_space()
    {
        var (window, _) = Show(AreaLayout.Default(), kind => kind != AreaKind.Documents);

        Assert.False(_areas[AreaKind.Documents].IsEffectivelyVisible);
        Assert.Equal(700, _areas[AreaKind.Terminals].Bounds.Height);
        window.Close();
    }

    [AvaloniaFact]
    public void Moves_the_areas_when_the_layout_changes()
    {
        var (window, view) = Show(AreaLayout.Default(), _ => true);

        view.Show(AreaLayout.FromSide(Datapoint.Terminal.Shell.TerminalSide.Left), _ => true);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(641, _areas[AreaKind.Agent].TranslatePoint(default, window)?.X);
        window.Close();
    }

    [AvaloniaFact]
    public void Gives_a_collapsed_area_only_the_size_it_asks_for_beside_the_agent()
    {
        _areas[AreaKind.Terminals] = new Border { Width = 40 };
        var (window, view) = Show(AreaLayout.Default(), kind => kind != AreaKind.Documents, AreaKind.Terminals);

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
        var (window, view) = Show(AreaLayout.Default(), _ => true, AreaKind.Terminals);

        Assert.False(view.IsCollapsedBeside);
        Assert.Equal(36, _areas[AreaKind.Terminals].Bounds.Height);
        Assert.Equal(663, _areas[AreaKind.Documents].Bounds.Height);
        Assert.Equal(359, _areas[AreaKind.Agent].Bounds.Width);
        window.Close();
    }

    [AvaloniaFact]
    public void Moves_only_the_divider_when_only_sizes_change()
    {
        var (window, view) = Show(AreaLayout.Default(), _ => true);
        var parent = _areas[AreaKind.Terminals].Parent;
        var narrower = AreaLayout.Default();
        ((AreaSplit)narrower).Size = 500;

        view.Show(narrower, _ => true);
        Dispatcher.UIThread.RunJobs();

        Assert.Same(parent, _areas[AreaKind.Terminals].Parent);
        Assert.Equal(500, _areas[AreaKind.Terminals].Bounds.Width);
        Assert.Equal(500, ((AreaSplit)view.Shown!).Size);
        window.Close();
    }
}
