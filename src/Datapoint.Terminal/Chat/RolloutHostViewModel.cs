using System.Globalization;
using Datapoint.Terminal.Agent.Skills;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Chat;

// One host's row on the rollout card: its state in a word or two, and below it why it failed or how its check is going.
public sealed class RolloutHostViewModel(RolloutHost host, TimeSpan healthyFor, bool isNext, bool isFirst)
{
    public string Name => host.Name;

    public bool IsFirst => isFirst;

    public bool HasSeparator => !isFirst;

    public string StateText => host.State switch
    {
        RolloutHostState.Waiting => isNext ? Strings.RolloutHostNext : Strings.RolloutHostWaiting,
        RolloutHostState.Working => Strings.RolloutHostWorking,
        RolloutHostState.Checking => Strings.RolloutHostChecking,
        RolloutHostState.Reconnecting => Strings.RolloutHostReconnecting,
        RolloutHostState.Done => host is { StartedAt: { } start, EndedAt: { } end } ? Strings.Format(Strings.RolloutHostDoneIn, Elapsed(end - start)) : Strings.RolloutHostDone,
        RolloutHostState.Failed => Strings.RolloutHostFailed,
        _ => Strings.RolloutHostSkipped,
    };

    public string? Detail => host.State == RolloutHostState.Checking && host.HealthyFor is { } healthy
        ? Strings.Format(Strings.RolloutHealthyOf, Clock(healthy), Clock(healthyFor))
        : host.Note is { Length: > 0 } note ? char.ToUpper(note[0], CultureInfo.CurrentCulture) + note[1..] : null;

    public bool HasDetail => Detail is not null;

    public bool IsCheckingHealth => host is { State: RolloutHostState.Checking, HealthyFor: not null } && healthyFor > TimeSpan.Zero;

    public double HealthPercent => host.HealthyFor is { } healthy && healthyFor > TimeSpan.Zero ? Math.Min(100, 100 * healthy / healthyFor) : 0;

    public bool IsWaiting => host.State is RolloutHostState.Waiting or RolloutHostState.Skipped;

    public bool IsRunning => host.State is RolloutHostState.Working or RolloutHostState.Checking or RolloutHostState.Reconnecting;

    public bool IsSucceeded => host.State == RolloutHostState.Done;

    public bool IsFailed => host.State == RolloutHostState.Failed;

    public string AccessibleName => Strings.Format(Strings.RolloutHostAccessible, Name, HasDetail ? StateText + ". " + Detail : StateText);

    // Minutes and seconds, as a check counts up, e.g. "1:20".
    private static string Clock(TimeSpan span) => ((int)span.TotalMinutes).ToString(CultureInfo.CurrentCulture) + ":" + span.Seconds.ToString("00", CultureInfo.CurrentCulture);

    private static string Elapsed(TimeSpan span) => span.TotalMinutes >= 1
        ? Strings.Format(Strings.RolloutMinutes, Math.Round(span.TotalMinutes))
        : Strings.Format(Strings.RolloutSeconds, Math.Max(1, (int)span.TotalSeconds));
}
