using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Datapoint.Terminal.Agent.Skills;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Chat;

// A skill's rollout as it goes (mock-ups 4 and 5): each host's state, the health check, and Pause and Stop. Pinned above the
// conversation while it runs, like a plan; once it's over it stays, folded to its result, where it started.
public sealed partial class RolloutProgressViewModel : ObservableObject
{
    private readonly Action<bool> _pause;
    private readonly Action _stop;
    private RolloutProgress _progress;

    public RolloutProgressViewModel(RolloutProgress progress, Action<bool> pause, Action stop)
    {
        ArgumentNullException.ThrowIfNull(progress);
        _progress = progress;
        _pause = pause;
        _stop = stop;
        HealthCheck = progress.Rollout.HealthCheck;
        Update(progress);
    }

    public Guid Id => _progress.Id;

    public string Title => Strings.Format(Strings.RolloutHeading, _progress.Title, OrderText(_progress));

    public ObservableCollection<RolloutHostViewModel> Hosts { get; } = [];

    public string? HealthCheck { get; }

    public bool HasHealthCheck => HealthCheck is not null;

    public bool HasNoHealthCheck => HealthCheck is null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInConversation))]
    public partial bool IsPinned { get; set; } = true;

    public bool IsInConversation => !IsPinned;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFolded), nameof(HeaderText), nameof(ToggleHelp), nameof(ChevronAngle))]
    public partial bool IsExpanded { get; set; } = true;

    public bool IsFolded => !IsExpanded;

    public double ChevronAngle => IsExpanded ? 180 : 0;

    public string HeaderText => IsExpanded ? Title : FoldedText;

    public string ToggleHelp => IsExpanded ? Strings.RolloutHideHosts : Strings.RolloutShowHosts;

    public bool IsRunning => !_progress.IsOver;

    public bool IsPaused => _progress.IsPaused;

    // Pausing lets the batch on now finish, so the button names where it will stop.
    public string PauseText => _progress.IsPaused ? Strings.RolloutResume : Strings.Format(Strings.RolloutPauseAfter, CurrentNames);

    public string StatusText => _progress.IsPaused
        ? Strings.Format(Strings.RolloutPausedAfter, CurrentNames)
        : Strings.Format(Strings.RolloutProgressText, _progress.Batch, _progress.BatchCount);

    public string FoldedText
    {
        get
        {
            var done = _progress.Hosts.Count(host => host.State == RolloutHostState.Done);
            var total = _progress.Hosts.Count;
            return _progress.Outcome switch
            {
                RolloutOutcome.Completed => Strings.Format(Strings.RolloutCompleted, _progress.Title, done, total),
                RolloutOutcome.Failed => Strings.Format(Strings.RolloutEndedFailed, _progress.Title, done, total),
                RolloutOutcome.Stopped => Strings.Format(Strings.RolloutEndedStopped, _progress.Title, done, total),
                _ => _progress.Title + " · " + StatusText,
            };
        }
    }

    // The batch being worked on, which a pause waits for.
    private string CurrentNames
    {
        get
        {
            var width = _progress.Rollout.Width(_progress.Hosts.Count);
            return string.Join(", ", _progress.Hosts.Skip((_progress.Batch - 1) * width).Take(width).Select(host => host.Name));
        }
    }

    public void Update(RolloutProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        _progress = progress;
        var width = progress.Rollout.Width(progress.Hosts.Count);
        var next = progress.Hosts.Skip(progress.Batch * width).Take(width).Select(host => host.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < progress.Hosts.Count; index++)
        {
            var host = new RolloutHostViewModel(progress.Hosts[index], progress.Rollout.HealthyFor, isNext: !progress.IsOver && next.Contains(progress.Hosts[index].Name), isFirst: index == 0);
            if (index < Hosts.Count)
            {
                Hosts[index] = host;
            }
            else
            {
                Hosts.Add(host);
            }
        }

        OnPropertyChanged(string.Empty);
        if (!progress.IsOver)
        {
            return;
        }

        IsPinned = false;
        IsExpanded = false;
    }

    private static string OrderText(RolloutProgress progress)
    {
        var count = progress.Hosts.Count == 1 ? Strings.RolloutOneHost : Strings.Format(Strings.RolloutHosts, progress.Hosts.Count);
        return progress.Rollout.Order switch
        {
            RolloutOrder.Batches => Strings.Format(Strings.RolloutInBatches, count, progress.Rollout.Width(progress.Hosts.Count)),
            RolloutOrder.AllAtOnce => Strings.Format(Strings.RolloutAllAtOnce, count),
            _ => Strings.Format(Strings.RolloutOneByOne, count),
        };
    }

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;

    [RelayCommand]
    private void Pause() => _pause(!_progress.IsPaused);

    [RelayCommand]
    private void Stop() => _stop();
}
