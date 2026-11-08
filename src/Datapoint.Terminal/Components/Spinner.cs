using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Datapoint.Terminal.Shell.Motion;

namespace Datapoint.Terminal.Components;

// Something in progress, as opposed to waiting: a blue arc turning on a faint ring. It holds still, as a quarter arc, when
// the user or the system asks to reduce motion, and turns only while it's shown.
public sealed class Spinner : Panel
{
    private readonly Arc _arc;

    public Spinner()
    {
        Width = 12;
        Height = 12;
        IsHitTestVisible = false;
        var ring = new Ellipse { StrokeThickness = 1.5 };
        ring.Bind(Shape.StrokeProperty, ring.GetResourceObservable("SeparatorBrush"));
        _arc = new Arc { StartAngle = -90, SweepAngle = 100, StrokeThickness = 1.5, StrokeLineCap = PenLineCap.Round, RenderTransform = new RotateTransform() };
        _arc.Classes.Add("spinner");
        _arc.Bind(Shape.StrokeProperty, _arc.GetResourceObservable("AccentBrush"));
        Children.Add(ring);
        Children.Add(_arc);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Update();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _arc.Classes.Set("turning", value: false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != IsVisibleProperty)
        {
            return;
        }

        Update();
    }

    // The turning itself is a style animation (Controls.axaml), which runs only while the class is set.
    private void Update() => _arc.Classes.Set("turning", IsVisible && VisualRoot is not null && MotionPreference.IsEnabled);
}
