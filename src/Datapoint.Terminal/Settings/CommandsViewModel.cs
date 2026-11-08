using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Settings;

// Settings › Commands (product decision 112): the commands the user taught the agent to treat as read-only, and the ones
// they approved this session that the check counted as changes, to teach with one click.
public sealed partial class CommandsViewModel : ObservableObject
{
    private readonly SettingsViewModel _settings;

    public CommandsViewModel(SettingsViewModel settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
        settings.Changed += (_, _) => Load();
        settings.RecentlyApprovedChanged += (_, _) => Load();
        Load();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLearned))]
    public partial IReadOnlyList<CommandEntryViewModel> Learned { get; private set; } = [];

    public bool HasLearned => Learned.Count > 0;

    // Approved this session and not taught yet.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRecent))]
    public partial IReadOnlyList<CommandEntryViewModel> Recent { get; private set; } = [];

    public bool HasRecent => Recent.Count > 0;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    public partial string Draft { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Status { get; private set; } = string.Empty;

    private bool CanAdd => Draft.Trim().Length > 0;

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private void Add()
    {
        if (!_settings.AddReadOnlyCommand(Draft))
        {
            Status = Strings.Format(Strings.CommandsCantTeach, Draft.Trim());
            return;
        }

        Status = Strings.Format(Strings.CommandsTaught, Teachable(Draft));
        Draft = string.Empty;
    }

    private void Remove(string command)
    {
        _settings.RemoveReadOnlyCommand(command);
        Status = Strings.Format(Strings.CommandsRemoved, command);
    }

    private void Teach(string command)
    {
        _settings.AddReadOnlyCommand(command);
        Status = Strings.Format(Strings.CommandsTaught, command);
    }

    private static string Teachable(string text) => Agent.Safety.LearnedCommands.Normalize(text) ?? text.Trim();

    private void Load()
    {
        var learned = _settings.LearnedCommands.Commands;
        Learned = [.. learned.Select((command, index) => new CommandEntryViewModel(command, index == 0, Remove))];
        Recent = [.. _settings.RecentlyApproved.Where(command => !learned.Contains(command, StringComparer.Ordinal))
            .Select((command, index) => new CommandEntryViewModel(command, index == 0, Teach))];
    }
}
