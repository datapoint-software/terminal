using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation;

namespace Datapoint.Terminal.Shell.Motion;

// Runs one animation at a time on a visual: a new one replaces the one running. Each is cut short once it should have ended,
// so nothing is left half faded, or a menu left open, when frames stop coming (as they do for a window in the background).
internal static class Playback
{
    private static readonly TimeSpan s_grace = TimeSpan.FromMilliseconds(100);
    private static readonly ConditionalWeakTable<Visual, CancellationTokenSource> s_running = [];

    // False when another animation took over before this one ended.
    public static async Task<bool> PlayAsync(Visual target, Animation animation)
    {
        Stop(target);
        var cancellation = new CancellationTokenSource();
        s_running.AddOrUpdate(target, cancellation);
        var deadline = Task.Delay(animation.Duration + s_grace, CancellationToken.None);
        await Task.WhenAny(animation.RunAsync(target, cancellation.Token), deadline).ConfigureAwait(true);
        return s_running.TryGetValue(target, out var current) && current == cancellation;
    }

    public static void Stop(Visual target)
    {
        if (!s_running.TryGetValue(target, out var running))
        {
            return;
        }

        s_running.Remove(target);
        running.Cancel();
        running.Dispose();
    }
}
