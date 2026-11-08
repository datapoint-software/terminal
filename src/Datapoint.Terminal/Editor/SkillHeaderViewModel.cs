using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Datapoint.Terminal.Agent.Skills;
using Datapoint.Terminal.Agent.Workspaces;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Editor;

// The form above a skill's steps (product decision 103): when to use it, the labels of the hosts it runs on, with the hosts
// they match now, and how it rolls out. Each change is written to the skill's header at once; one that doesn't read stays
// on screen with the reason and isn't written.
public sealed partial class SkillHeaderViewModel : LocalizedObject
{
    private const double MaxMinutes = 1440;

    private readonly Func<IReadOnlyList<WorkspaceConnection>> _hosts;
    private bool _isLoading;

    public SkillHeaderViewModel(Skill skill, string location, bool canWrite, Func<IReadOnlyList<WorkspaceConnection>> hosts)
    {
        ArgumentNullException.ThrowIfNull(skill);
        _hosts = hosts;
        Location = location;
        CanWrite = canWrite;
        Load(skill);
    }

    public string Name { get; private set; } = string.Empty;

    public string Location { get; }

    public bool CanWrite { get; }

    public ObservableCollection<string> Labels { get; } = [];

    public IReadOnlyList<string> OrderChoices { get; } = [Strings.SkillOrderOneByOne, Strings.SkillOrderBatches, Strings.SkillOrderAllAtOnce];

    public IReadOnlyList<string> FailureChoices { get; } = [Strings.SkillOnFailureStop, Strings.SkillOnFailureContinue, Strings.SkillOnFailureAsk];

    // Writes the new settings; returns false when the file changed on disk first.
    public Func<IReadOnlyList<string>, SkillRollout, string, bool>? Saving { get; set; }

    public Action<IReadOnlyList<string>>? RunRequested { get; set; }

    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewLabel { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBatches), nameof(IsAllAtOnce))]
    public partial int OrderIndex { get; set; }

    public bool IsBatches => OrderIndex == 1;

    public bool IsAllAtOnce => OrderIndex == 2;

    [ObservableProperty]
    public partial string BatchSize { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MaxHosts { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHealthCheck))]
    public partial string HealthCheck { get; set; } = string.Empty;

    public bool HasHealthCheck => HealthCheck.Trim().Length > 0;

    [ObservableProperty]
    public partial string HealthyFor { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Timeout { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FailureHint))]
    public partial int FailureIndex { get; set; }

    public string FailureHint => FailureIndex switch
    {
        1 => Strings.SkillOnFailureContinueHint,
        2 => Strings.SkillOnFailureAskHint,
        _ => Strings.SkillOnFailureStopHint,
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProblem))]
    public partial string? Problem { get; set; }

    public bool HasProblem => Problem is not null;

    public IReadOnlyList<string> MatchingHosts => [.. _hosts().Where(host => Labels.All(label => host.HasLabel(label))).Select(host => host.Name)];

    public string MatchText => Labels.Count == 0 ? Strings.SkillMatchesEvery
        : MatchingHosts is { Count: > 0 } hosts ? Strings.Format(Strings.SkillMatchesHosts, JoinNames(hosts))
        : Strings.SkillMatchesNone;

    public string RunText => MatchingHosts.Count switch
    {
        0 => Strings.SkillRunOnNone,
        1 => Strings.SkillRunOnOne,
        var count => Strings.Format(Strings.SkillRunOn, count),
    };

    public void Load(Skill skill)
    {
        ArgumentNullException.ThrowIfNull(skill);
        _isLoading = true;
        Name = skill.Title;
        Description = skill.Description;
        Labels.Clear();
        foreach (var label in skill.Labels)
        {
            Labels.Add(label);
        }

        var rollout = skill.Rollout;
        OrderIndex = (int)rollout.Order;
        BatchSize = rollout.BatchSize.ToString(CultureInfo.CurrentCulture);
        MaxHosts = rollout.MaxHosts.ToString(CultureInfo.CurrentCulture);
        HealthCheck = rollout.HealthCheck ?? string.Empty;
        HealthyFor = Minutes(rollout.HealthyFor);
        Timeout = Minutes(rollout.Timeout);
        FailureIndex = rollout.OnFailure switch
        {
            RolloutFailure.Continue => 1,
            RolloutFailure.Ask => 2,
            _ => 0,
        };
        Problem = null;
        _isLoading = false;
        RefreshHosts();
    }

    // The workspace's hosts or their labels changed.
    public void RefreshHosts()
    {
        OnPropertyChanged(nameof(MatchingHosts));
        OnPropertyChanged(nameof(MatchText));
        OnPropertyChanged(nameof(RunText));
    }

    // Text fields are written when the user leaves them or presses Enter, not on every key.
    [RelayCommand]
    public void Commit() => Save();

    partial void OnOrderIndexChanged(int value) => Save();

    partial void OnFailureIndexChanged(int value) => Save();

    [RelayCommand]
    private void AddLabel()
    {
        var label = NewLabel.Trim();
        if (label.Length == 0)
        {
            return;
        }

        if (!WorkspaceConnection.IsPlainLabel(label))
        {
            Problem = Strings.SkillLabelProblem;
            return;
        }

        if (!Labels.Contains(label, StringComparer.OrdinalIgnoreCase))
        {
            Labels.Add(label);
        }

        NewLabel = string.Empty;
        Save();
        RefreshHosts();
    }

    [RelayCommand]
    private void RemoveLabel(string label)
    {
        Labels.Remove(label);
        Save();
        RefreshHosts();
    }

    [RelayCommand]
    private void Run() => RunRequested?.Invoke(MatchingHosts);

    private void Save()
    {
        if (_isLoading || !CanWrite)
        {
            return;
        }

        if (Rollout() is not { } rollout)
        {
            Problem = Strings.SkillNumberProblem;
            return;
        }

        Problem = Saving?.Invoke([.. Labels], rollout, Description.Trim()) == false ? Strings.SkillSaveConflict : null;
    }

    private SkillRollout? Rollout()
    {
        var batch = Count(BatchSize);
        var most = Count(MaxHosts);
        var healthyFor = Duration(HealthyFor);
        var timeout = Duration(Timeout);
        if (batch is null || most is null || healthyFor is null || timeout is null)
        {
            return null;
        }

        return new SkillRollout
        {
            Order = (RolloutOrder)Math.Clamp(OrderIndex, 0, 2),
            BatchSize = batch.Value,
            MaxHosts = most.Value,
            HealthCheck = HasHealthCheck ? HealthCheck.Trim() : null,
            HealthyFor = healthyFor.Value,
            Timeout = timeout.Value,
            OnFailure = FailureIndex switch
            {
                1 => RolloutFailure.Continue,
                2 => RolloutFailure.Ask,
                _ => RolloutFailure.Stop,
            },
        };
    }

    private static int? Count(string text) =>
        int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.CurrentCulture, out var count) && count is > 0 and <= 1000 ? count : null;

    // Either decimal separator, whatever the culture says, since "1.5" and "1,5" can only mean one thing here.
    private static TimeSpan? Duration(string text) =>
        double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var minutes) && minutes is > 0 and <= MaxMinutes
            ? TimeSpan.FromSeconds(Math.Round(minutes * 60))
            : null;

    private static string Minutes(TimeSpan span) => Math.Round(span.TotalMinutes, 2).ToString(CultureInfo.CurrentCulture);

    private static string JoinNames(IReadOnlyList<string> names) =>
        names.Count == 1 ? names[0] : string.Join(", ", names.Take(names.Count - 1)) + " " + Strings.And + " " + names[^1];
}
