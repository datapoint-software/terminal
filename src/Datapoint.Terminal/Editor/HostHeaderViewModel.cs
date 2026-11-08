using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Datapoint.Terminal.Agent.Hosts;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Editor;

// The line above a host's documentation, with its environment, labels, address and open terminals and a way to open one; and
// its maintenance log below, newest first, a few entries until the user asks for all.
public sealed partial class HostHeaderViewModel(string name, string? environment, string? address, Action openTerminal) : ObservableObject
{
    public string Name { get; } = name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEnvironment), nameof(IsProduction))]
    public partial string? Environment { get; set; } = environment;

    public bool HasEnvironment => Environment is not null;

    public bool IsProduction => Agent.EnvironmentLabels.IsProduction(Environment);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLabels))]
    public partial IReadOnlyList<string> Labels { get; set; } = [];

    public bool HasLabels => Labels.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAddress))]
    public partial string? Address { get; set; } = address;

    public bool HasAddress => !string.IsNullOrEmpty(Address);

    // Null where the workspace can't be changed, e.g. a read-only folder, and the button isn't shown.
    public Action? EditHost { get; init; }

    public bool CanEdit => EditHost is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOpenText))]
    public partial string OpenText { get; set; } = string.Empty;

    public bool HasOpenText => OpenText.Length > 0;

    public const int ShortLogLength = 3;

    private IReadOnlyList<HostLogItem> _log = [];

    public ObservableCollection<HostLogItem> LogEntries { get; } = [];

    public bool HasLog => _log.Count > 0;

    public bool CanShowAll => _log.Count > ShortLogLength;

    public string LogHeading => Strings.Format(Strings.DocumentLogLabel, Name);

    public string ShowAllText => IsShowingAll ? Strings.DocumentLogShowFewer : Strings.Format(Strings.DocumentLogShowAll, _log.Count);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowAllText))]
    public partial bool IsShowingAll { get; set; }

    public void SetLog(IReadOnlyList<HostLogEntry> entries, string userName)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _log = [.. entries.Select(entry => HostLogItem.From(entry, userName))];
        ShowLog();
        OnPropertyChanged(nameof(HasLog));
        OnPropertyChanged(nameof(CanShowAll));
        OnPropertyChanged(nameof(ShowAllText));
    }

    partial void OnIsShowingAllChanged(bool value) => ShowLog();

    private void ShowLog()
    {
        LogEntries.Clear();
        foreach (var item in IsShowingAll ? _log : _log.Take(ShortLogLength))
        {
            LogEntries.Add(item);
        }
    }

    [RelayCommand]
    private void ToggleLog() => IsShowingAll = !IsShowingAll;

    [RelayCommand]
    private void OpenTerminal() => openTerminal();

    [RelayCommand]
    private void Edit() => EditHost?.Invoke();
}
