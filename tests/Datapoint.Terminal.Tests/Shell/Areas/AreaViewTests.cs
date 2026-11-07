using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
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

    private (Window Window, AreaView View) Show(AreaNode layout, Func<AreaKind, bool> isShown)
    {
        var host = new Panel();
        var view = new AreaView(host, _areas);
        view.Show(layout, isShown);
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
}
