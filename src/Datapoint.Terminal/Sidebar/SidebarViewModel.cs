using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Datapoint.Terminal.Agent.Workspaces;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Sidebar;

// Product decision 100: the workspace, its hosts, its skills and its instructions, by name. The sidebar opens and edits what
// the application created; it doesn't create, rename or delete files. The window says what happens when one is chosen.
public sealed partial class SidebarViewModel(string workspaceFolder) : ObservableObject
{
    public string WorkspaceFolder { get; } = workspaceFolder;

    public string SkillsFolder => Path.Combine(WorkspaceFolder, "skills");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WorkspaceInitials), nameof(WorkspaceAccessibleName))]
    public partial string WorkspaceName { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WorkspaceAccessibleName))]
    public partial string WorkspaceDetail { get; set; } = string.Empty;

    public string WorkspaceInitials => WorkspaceLocation.Initials(WorkspaceName);

    public string WorkspaceAccessibleName => Strings.Format(Strings.SidebarSwitcherName, WorkspaceName, WorkspaceDetail);

    public ObservableCollection<HostItemViewModel> Hosts { get; } = [];

    public bool HasHosts => Hosts.Count > 0;

    // Hosts in the user's SSH configuration that the workspace doesn't list, folded into one line until asked for.
    public ObservableCollection<SshHostItemViewModel> OtherHosts { get; } = [];

    public bool HasOtherHosts => OtherHosts.Count > 0;

    public string OtherHostsText => Strings.Format(Strings.SidebarOtherHosts, OtherHosts.Count);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OtherHostsChevronAngle))]
    public partial bool IsOtherHostsExpanded { get; set; }

    public double OtherHostsChevronAngle => IsOtherHostsExpanded ? 90 : 0;

    public ObservableCollection<SkillItemViewModel> Skills { get; } = [];

    public bool HasSkills => Skills.Count > 0;

    [ObservableProperty]
    public partial HostItemViewModel? SelectedHost { get; set; }

    [ObservableProperty]
    public partial SkillItemViewModel? SelectedSkill { get; set; }

    public Action<WorkspaceConnection>? HostChosen { get; set; }

    public Action<WorkspaceConnection>? TerminalRequested { get; set; }

    public Action<WorkspaceConnection>? HostRemoved { get; set; }

    public Action<WorkspaceConnection>? HostEditRequested { get; set; }

    public Action<string>? HostAdded { get; set; }

    public Action<SkillItemViewModel>? SkillChosen { get; set; }

    public Action? InstructionsRequested { get; set; }

    public Action? NewHostRequested { get; set; }

    public Action? SettingsRequested { get; set; }

    // The workspaces the switcher offers, read when it opens.
    public Func<IReadOnlyList<WorkspaceSwitchItem>>? ListWorkspaces { get; set; }

    public Action<WorkspaceSwitchItem>? WorkspaceChosen { get; set; }

    public Action? NewWorkspaceRequested { get; set; }

    public Action? OpenWorkspaceRequested { get; set; }

    public Action? RevealRequested { get; set; }

    public void SetWorkspace(string name, string detail)
    {
        WorkspaceName = name;
        WorkspaceDetail = detail;
    }

    public void SetHosts(IReadOnlyList<WorkspaceConnection> connections, IReadOnlyList<string> otherAliases)
    {
        ArgumentNullException.ThrowIfNull(connections);
        ArgumentNullException.ThrowIfNull(otherAliases);
        var selected = SelectedHost?.Name;
        var open = Hosts.ToDictionary(host => host.Name, host => host.OpenTerminals, StringComparer.OrdinalIgnoreCase);
        Hosts.Clear();
        foreach (var connection in connections)
        {
            Hosts.Add(new HostItemViewModel(connection) { OpenTerminals = open.GetValueOrDefault(connection.Name) });
        }

        OtherHosts.Clear();
        foreach (var alias in otherAliases)
        {
            OtherHosts.Add(new SshHostItemViewModel(alias, AddHost));
        }

        SelectedHost = Hosts.FirstOrDefault(host => string.Equals(host.Name, selected, StringComparison.OrdinalIgnoreCase));
        OnPropertyChanged(nameof(HasHosts));
        OnPropertyChanged(nameof(HasOtherHosts));
        OnPropertyChanged(nameof(OtherHostsText));
    }

    // How many tabs are open to each host, by connection name.
    public void SetOpenTerminals(IReadOnlyDictionary<string, int> counts)
    {
        ArgumentNullException.ThrowIfNull(counts);
        foreach (var host in Hosts)
        {
            host.OpenTerminals = counts.GetValueOrDefault(host.Name);
        }
    }

    // The tab in view marks its host, so the sidebar shows where the user is.
    public void ShowHost(string? connection) =>
        SelectedHost = Hosts.FirstOrDefault(host => string.Equals(host.Name, connection, StringComparison.OrdinalIgnoreCase));

    public void RefreshSkills()
    {
        var selected = SelectedSkill?.Path;
        var skills = SkillListing.List(SkillsFolder);
        if (skills.SequenceEqual(Skills))
        {
            return;
        }

        Skills.Clear();
        foreach (var skill in skills)
        {
            Skills.Add(skill);
        }

        SelectedSkill = Skills.FirstOrDefault(skill => string.Equals(skill.Path, selected, StringComparison.Ordinal));
        OnPropertyChanged(nameof(HasSkills));
    }

    [RelayCommand]
    private void ToggleOtherHosts() => IsOtherHostsExpanded = !IsOtherHostsExpanded;

    [RelayCommand]
    private void ChooseHost(HostItemViewModel host) => HostChosen?.Invoke(host.Connection);

    [RelayCommand]
    private void OpenTerminal(HostItemViewModel host) => TerminalRequested?.Invoke(host.Connection);

    [RelayCommand]
    private void RemoveHost(HostItemViewModel host) => HostRemoved?.Invoke(host.Connection);

    [RelayCommand]
    private void EditHost(HostItemViewModel host) => HostEditRequested?.Invoke(host.Connection);

    [RelayCommand]
    private void ChooseSkill(SkillItemViewModel skill) => SkillChosen?.Invoke(skill);

    [RelayCommand]
    private void OpenInstructions() => InstructionsRequested?.Invoke();

    [RelayCommand]
    private void NewHost() => NewHostRequested?.Invoke();

    [RelayCommand]
    private void OpenSettings() => SettingsRequested?.Invoke();

    private void AddHost(string alias) => HostAdded?.Invoke(alias);
}
