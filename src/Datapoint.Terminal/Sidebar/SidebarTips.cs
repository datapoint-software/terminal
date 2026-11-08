using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Sidebar;

// Product decision 147: a short tour of the sidebar, one tip at a time beside what it describes. Each tip takes focus, so a
// keyboard or screen reader user hears it; Escape, Skip tips or a click elsewhere ends the tour, and focus goes back.
internal sealed class SidebarTips(IReadOnlyList<(Control Target, string Title, string Text)> tips, IInputElement? focusBefore, Action done)
{
    private Flyout? _flyout;
    private bool _isMoving;
    private bool _isEnded;
    private int _index;

    public string? CurrentTitle => _flyout is null ? null : tips[_index].Title;

    public void Start() => Show(0);

    public void Next() => Show(_index + 1);

    public void End()
    {
        if (_isEnded)
        {
            return;
        }

        _isEnded = true;
        Replace(null);
        focusBefore?.Focus();
        done();
    }

    private void Show(int index)
    {
        // A tip whose part of the sidebar isn't shown is passed over.
        while (index < tips.Count && !tips[index].Target.IsEffectivelyVisible)
        {
            index++;
        }

        if (index >= tips.Count)
        {
            End();
            return;
        }

        _index = index;
        var isLast = !tips.Skip(index + 1).Any(tip => tip.Target.IsEffectivelyVisible);
        var next = new Button { Content = isLast ? Strings.TipDone : Strings.TipNext };
        var target = tips[index].Target;
        var flyout = new Flyout
        {
            Content = Bubble(Content(tips[index].Title, tips[index].Text, index, isLast, next)),
            Placement = PlacementMode.RightEdgeAlignedTop,

            // The arrow sits below the bubble's rounded corner, so the bubble moves up to bring it level with the part's middle.
            VerticalOffset = (target.Bounds.Height / 2) - ArrowTop - (ArrowHeight / 2),
        };
        flyout.FlyoutPresenterClasses.Add("tip");
        flyout.Opened += (_, _) => next.Focus();

        // Closed other than by the tour, as by a click elsewhere, ends it.
        flyout.Closed += (_, _) => OnClosed(flyout);
        Replace(flyout);
        flyout.ShowAt(target);
    }

    private void OnClosed(Flyout flyout)
    {
        if (_isMoving || !ReferenceEquals(_flyout, flyout))
        {
            return;
        }

        End();
    }

    private void Replace(Flyout? flyout)
    {
        var previous = _flyout;
        _flyout = flyout;
        _isMoving = true;
        previous?.Hide();
        _isMoving = false;
    }

    private static Grid Bubble(StackPanel content)
    {
        var fill = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse("M9,0 L1,8 L9,16 Z") };
        fill.Bind(Avalonia.Controls.Shapes.Shape.FillProperty, fill.GetResourceObservable("CardBrush"));
        var edge = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse("M9,0 L1,8 L9,16"), StrokeThickness = 1 };
        edge.Bind(Avalonia.Controls.Shapes.Shape.StrokeProperty, edge.GetResourceObservable("SeparatorBrush"));
        var arrow = new Panel { Width = 9, Height = ArrowHeight, VerticalAlignment = VerticalAlignment.Top, Margin = new Avalonia.Thickness(0, ArrowTop, -1, 0), ZIndex = 1, Children = { fill, edge } };
        var bubble = new Border { Child = content, Padding = new Avalonia.Thickness(14, 12), CornerRadius = new Avalonia.CornerRadius(8), BorderThickness = new Avalonia.Thickness(1) };
        bubble.Bind(Border.BackgroundProperty, bubble.GetResourceObservable("CardBrush"));
        bubble.Bind(Border.BorderBrushProperty, bubble.GetResourceObservable("SeparatorBrush"));
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Avalonia.Thickness(2, 0, 0, 0) };
        grid.Children.Add(arrow);
        grid.Children.Add(bubble);
        Grid.SetColumn(bubble, 1);
        return grid;
    }

    private const double ArrowHeight = 16;

    private const double ArrowTop = 12;

    private StackPanel Content(string title, string text, int index, bool isLast, Button next)
    {
        next.Classes.Add("primary");
        AutomationProperties.SetHelpText(next, title + ". " + text);
        next.Click += (_, _) => Next();
        var skip = new Button { Content = Strings.TipSkip, IsVisible = !isLast };
        skip.Classes.Add("plain");
        skip.Click += (_, _) => End();
        var body = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, LineHeight = 18 };
        body.Classes.Add("secondary");
        AutomationProperties.SetLiveSetting(body, AutomationLiveSetting.Polite);
        var count = new TextBlock { Text = Strings.Format(Strings.TipCount, index + 1, tips.Count), VerticalAlignment = VerticalAlignment.Center };
        count.Classes.Add("secondary");
        var buttons = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 6, Margin = new Avalonia.Thickness(0, 6, 0, 0) };
        buttons.Children.Add(count);
        buttons.Children.Add(skip);
        buttons.Children.Add(next);
        Grid.SetColumn(skip, 1);
        Grid.SetColumn(next, 2);
        var content = new StackPanel
        {
            Spacing = 4,
            Width = 280,
            Children = { new TextBlock { Text = title, FontWeight = FontWeight.SemiBold }, body, buttons },
        };
        AutomationProperties.SetName(content, title);
        content.KeyDown += (_, e) => OnKeyDown(e);
        return content;
    }

    private void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        End();
        e.Handled = true;
    }
}
