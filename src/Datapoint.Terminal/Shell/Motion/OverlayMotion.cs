using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;

namespace Datapoint.Terminal.Shell.Motion;

// Every popup (menus, drop-downs, tooltips, flyouts) fades and grows in as it opens. Menus and flyouts also fade out as they
// close: their closing is put off until the fade ends, then let through. A drop-down or tooltip closes at once, as Avalonia
// can't put that off. Nothing moves while MotionPreference says not to.
public static class OverlayMotion
{
    private static readonly TimeSpan s_openDuration = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan s_closeDuration = TimeSpan.FromMilliseconds(100);
    private static readonly ConditionalWeakTable<PopupFlyoutBase, FlyoutState> s_flyouts = [];

    private static bool s_isStarted;

    public static void Start()
    {
        if (s_isStarted)
        {
            return;
        }

        s_isStarted = true;
        Popup.IsOpenProperty.Changed.AddClassHandler<Popup>(OnPopupOpenChanged);
        FlyoutBase.IsOpenProperty.Changed.AddClassHandler<PopupFlyoutBase>(OnFlyoutOpenChanged);
    }

    private static void OnPopupOpenChanged(Popup popup, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is not true || popup.Child is not { } child)
        {
            return;
        }

        // Also clears what a fade-out left, so the popup never reopens invisible.
        Playback.Stop(child);
        child.ClearValue(Visual.OpacityProperty);
        if (!MotionPreference.IsEnabled)
        {
            return;
        }

        _ = FadeInAsync(child);
    }

    private static async Task FadeInAsync(Control child)
    {
        if (!await Playback.PlayAsync(child, Fade(from: 0, to: 1, s_openDuration, new CubicEaseOut(), FillMode.Backward)).ConfigureAwait(true))
        {
            return;
        }

        Playback.Stop(child);
    }

    private static void OnFlyoutOpenChanged(PopupFlyoutBase flyout, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is not true)
        {
            return;
        }

        var state = s_flyouts.GetValue(flyout, _ => new FlyoutState());
        state.IsClosing = false;
        if (state.IsWatched)
        {
            return;
        }

        state.IsWatched = true;
        flyout.Closing += OnFlyoutClosing;
    }

    private static void OnFlyoutClosing(object? sender, CancelEventArgs e)
    {
        if (e.Cancel || sender is not PopupFlyoutBase flyout || !s_flyouts.TryGetValue(flyout, out var state) || state.CanClose)
        {
            return;
        }

        if (state.IsClosing)
        {
            e.Cancel = true;
            return;
        }

        if (flyout.Popup.Child is not { } child || !MotionPreference.IsEnabled)
        {
            return;
        }

        e.Cancel = true;
        state.IsClosing = true;
        child.IsHitTestVisible = false;
        _ = FadeOutAsync(flyout, child, state);
    }

    private static async Task FadeOutAsync(PopupFlyoutBase flyout, Control child, FlyoutState state)
    {
        // Held at nothing once the fade ends, so the menu doesn't flash back before it closes.
        child.Opacity = 0;
        var isCurrent = await Playback.PlayAsync(child, Fade(from: 1, to: 0, s_closeDuration, new CubicEaseIn(), FillMode.None)).ConfigureAwait(true);
        child.IsHitTestVisible = true;
        if (state.IsClosing)
        {
            state.IsClosing = false;
            state.CanClose = true;
            flyout.Hide();
            state.CanClose = false;
        }

        if (!isCurrent)
        {
            return;
        }

        Playback.Stop(child);
    }

    private static Animation Fade(double from, double to, TimeSpan duration, Easing easing, FillMode fill) => new()
    {
        Duration = duration,
        Easing = easing,
        FillMode = fill,
        Children =
        {
            Frame(0, from),
            Frame(1, to),
        },
    };

    // Grows from 97 %, enough to read as opening without the menu seeming to move.
    private static KeyFrame Frame(double cue, double opacity) => new()
    {
        Cue = new Cue(cue),
        Setters =
        {
            new Setter(Visual.OpacityProperty, opacity),
            new Setter(ScaleTransform.ScaleXProperty, opacity > 0 ? 1d : 0.97),
            new Setter(ScaleTransform.ScaleYProperty, opacity > 0 ? 1d : 0.97),
        },
    };

    private sealed class FlyoutState
    {
        public bool IsWatched { get; set; }

        public bool IsClosing { get; set; }

        public bool CanClose { get; set; }
    }
}
