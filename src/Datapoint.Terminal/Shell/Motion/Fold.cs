using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace Datapoint.Terminal.Shell.Motion;

// Shows or hides part of the window by folding it: its height and opacity ease from or to nothing, so what's below slides
// rather than jumps. Set in place of IsVisible; while MotionPreference says not to move, or before the part is on screen,
// it's IsVisible.
public sealed class Fold : AvaloniaObject
{
    public static readonly AttachedProperty<bool> IsOpenProperty =
        AvaloniaProperty.RegisterAttached<Fold, Control, bool>("IsOpen", defaultValue: true);

    private static readonly TimeSpan s_duration = TimeSpan.FromMilliseconds(200);

    // A pinned card's content goes as it's told to fold away, so the last content is kept to fold.
    private static readonly ConditionalWeakTable<ContentControl, object> s_lastContent = [];

    static Fold()
    {
        IsOpenProperty.Changed.AddClassHandler<Control>(OnIsOpenChanged);
        ContentControl.ContentProperty.Changed.AddClassHandler<ContentControl>(OnContentChanged);
    }

    private Fold()
    {
    }

    public static bool GetIsOpen(Control control) => control?.GetValue(IsOpenProperty) ?? throw new ArgumentNullException(nameof(control));

    public static void SetIsOpen(Control control, bool value) => control?.SetValue(IsOpenProperty, value);

    private static void OnContentChanged(ContentControl control, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is null || !control.IsSet(IsOpenProperty))
        {
            return;
        }

        s_lastContent.AddOrUpdate(control, e.NewValue);
    }

    private static void OnIsOpenChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        var isOpen = e.NewValue is true;
        if (control is ContentControl { Content: { } current } holder)
        {
            s_lastContent.AddOrUpdate(holder, current);
        }

        Playback.Stop(control);
        control.ClearValue(Layoutable.HeightProperty);
        control.ClearValue(Visual.OpacityProperty);
        control.ClearValue(Visual.ClipToBoundsProperty);
        if (!MotionPreference.IsEnabled || TopLevel.GetTopLevel(control) is null || (!isOpen && !control.IsVisible))
        {
            control.IsVisible = isOpen;
            return;
        }

        _ = isOpen ? OpenAsync(control) : CloseAsync(control);
    }

    private static async Task OpenAsync(Control control)
    {
        control.IsVisible = true;
        var width = (control.Parent as Control)?.Bounds.Width ?? control.Bounds.Width;
        control.Measure(new Size(Math.Max(0, width - control.Margin.Left - control.Margin.Right), double.PositiveInfinity));
        var height = Math.Max(0, control.DesiredSize.Height - control.Margin.Top - control.Margin.Bottom);
        control.ClipToBounds = true;
        if (!await Playback.PlayAsync(control, Animate(from: 0, to: height, new CubicEaseOut(), FillMode.Backward)).ConfigureAwait(true))
        {
            return;
        }

        Playback.Stop(control);
        control.ClearValue(Visual.ClipToBoundsProperty);
    }

    private static async Task CloseAsync(Control control)
    {
        var kept = control as ContentControl;
        if (kept is { Content: null } && s_lastContent.TryGetValue(kept, out var last))
        {
            kept.SetCurrentValue(ContentControl.ContentProperty, last);
        }
        else
        {
            kept = null;
        }

        // Held folded once the animation ends, so the part doesn't flash back before it's hidden.
        var height = control.Bounds.Height;
        control.Height = 0;
        control.Opacity = 0;
        control.ClipToBounds = true;
        if (!await Playback.PlayAsync(control, Animate(from: height, to: 0, new CubicEaseIn(), FillMode.None)).ConfigureAwait(true))
        {
            return;
        }

        Playback.Stop(control);
        control.IsVisible = false;
        control.ClearValue(Layoutable.HeightProperty);
        control.ClearValue(Visual.OpacityProperty);
        control.ClearValue(Visual.ClipToBoundsProperty);
        if (kept is null || GetIsOpen(kept))
        {
            return;
        }

        s_lastContent.Remove(kept);
        kept.SetCurrentValue(ContentControl.ContentProperty, null);
    }

    private static Animation Animate(double from, double to, Easing easing, FillMode fill) => new()
    {
        Duration = s_duration,
        Easing = easing,
        FillMode = fill,
        Children =
        {
            new KeyFrame { Cue = new Cue(0), Setters = { new Setter(Layoutable.HeightProperty, from), new Setter(Visual.OpacityProperty, from > to ? 1d : 0d) } },
            new KeyFrame { Cue = new Cue(1), Setters = { new Setter(Layoutable.HeightProperty, to), new Setter(Visual.OpacityProperty, from > to ? 0d : 1d) } },
        },
    };
}
