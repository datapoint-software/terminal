using CommunityToolkit.Mvvm.ComponentModel;

namespace Datapoint.Terminal.Chat;

// One command the agent ran while looking: running until it finishes, then done or failed.
public sealed partial class LookCommandViewModel(string command) : ObservableObject
{
    public string Command => command;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDone), nameof(IsFailed))]
    public partial bool IsRunning { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDone))]
    public partial bool IsFailed { get; set; }

    public bool IsDone => !IsRunning && !IsFailed;
}
