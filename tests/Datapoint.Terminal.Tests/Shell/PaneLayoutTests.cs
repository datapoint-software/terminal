using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Datapoint.Terminal.Shell;

namespace Datapoint.Terminal.Tests.Shell;

public sealed class PaneLayoutTests
{
    private readonly Border[] _panes = [new() { Background = Brushes.Black }, new() { Background = Brushes.Navy }, new() { Background = Brushes.Teal }];
    private readonly Grid _host = new();

    public PaneLayoutTests()
    {
        _host.Children.AddRange(_panes);
    }

    private Window Show()
    {
        var window = new Window { Width = 901, Height = 600, Content = _host };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void Shows_one_terminal_alone_without_an_accent()
    {
        var layout = new PaneLayout(_host);
        layout.Apply(_panes, [_panes[1]], beside: true, active: 0);
        var window = Show();

        Assert.False(_panes[0].IsVisible);
        Assert.Equal(901, _panes[1].Bounds.Width);
        Assert.False(layout.ActiveLine.IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void Puts_panes_side_by_side_with_a_hairline_between()
    {
        var layout = new PaneLayout(_host);
        layout.Apply(_panes, [_panes[0], _panes[2]], beside: true, active: 1);
        var window = Show();

        Assert.False(_panes[1].IsVisible);
        Assert.Equal(new Avalonia.Rect(0, 0, 450, 600), _panes[0].Bounds);
        Assert.Equal(new Avalonia.Rect(451, 0, 450, 600), _panes[2].Bounds);
        Assert.True(layout.ActiveLine.IsVisible);
        Assert.Equal(451, layout.ActiveLine.Bounds.X);
        Assert.Equal(2, layout.ActiveLine.Bounds.Height);
        window.Close();
    }

    [AvaloniaFact]
    public void Stacks_panes_and_restores_their_sizes()
    {
        var layout = new PaneLayout(_host);
        layout.Apply(_panes, _panes, beside: false, active: 0, sizes: [150, 250, 198]);
        var window = Show();

        Assert.Equal([150, 250, 198], layout.Measure());
        Assert.Equal(151, _panes[1].Bounds.Y);
        Assert.Equal(901, _panes[2].Bounds.Width);
        window.Close();
    }

    [AvaloniaFact]
    public void Ignores_sizes_for_a_different_number_of_panes()
    {
        var layout = new PaneLayout(_host);
        layout.Apply(_panes, [_panes[0], _panes[1]], beside: true, active: 0, sizes: [100, 300, 200]);
        var window = Show();

        Assert.Equal(450, _panes[0].Bounds.Width);
        window.Close();
    }
}
