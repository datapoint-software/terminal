using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Datapoint.Terminal.Agent.Projects;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Sidebar;

// Product decision 98: the project's hosts, then its files. The window says what happens when one is chosen.
public sealed partial class SidebarViewModel(string projectFolder, string hostsFolder) : ObservableObject
{
    public string ProjectFolder { get; } = projectFolder;

    public string HostsFolder { get; } = hostsFolder;

    public ObservableCollection<HostItemViewModel> Hosts { get; } = [];

    public bool HasHosts => Hosts.Count > 0;

    // Hosts in the user's SSH configuration that the project doesn't list, folded into one line until asked for.
    public ObservableCollection<SshHostItemViewModel> OtherHosts { get; } = [];

    public bool HasOtherHosts => OtherHosts.Count > 0;

    public string OtherHostsText => Strings.Format(Strings.SidebarOtherHosts, OtherHosts.Count);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OtherHostsChevronAngle))]
    public partial bool IsOtherHostsExpanded { get; set; }

    public double OtherHostsChevronAngle => IsOtherHostsExpanded ? 90 : 0;

    public ObservableCollection<FileNodeViewModel> Files { get; } = [];

    [ObservableProperty]
    public partial HostItemViewModel? SelectedHost { get; set; }

    [ObservableProperty]
    public partial FileNodeViewModel? SelectedFile { get; set; }

    public Action<ProjectConnection>? HostChosen { get; set; }

    public Action<ProjectConnection>? TerminalRequested { get; set; }

    public Action<string>? HostAdded { get; set; }

    public Action<FileNodeViewModel>? FileChosen { get; set; }

    public Action? NewFileRequested { get; set; }

    public void SetHosts(IReadOnlyList<ProjectConnection> connections, IReadOnlyList<string> otherAliases)
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

    public void RefreshFiles()
    {
        FileNodeViewModel.Merge(Files, ProjectListing.List(ProjectFolder, ProjectFolder), ProjectFolder, HostsFolder);
        foreach (var node in Files)
        {
            node.Refresh();
        }
    }

    [RelayCommand]
    private void ToggleOtherHosts() => IsOtherHostsExpanded = !IsOtherHostsExpanded;

    [RelayCommand]
    private void ChooseHost(HostItemViewModel host) => HostChosen?.Invoke(host.Connection);

    [RelayCommand]
    private void OpenTerminal(HostItemViewModel host) => TerminalRequested?.Invoke(host.Connection);

    [RelayCommand]
    private void ChooseFile(FileNodeViewModel node)
    {
        if (node.IsFolder)
        {
            node.IsExpanded = !node.IsExpanded;
            return;
        }

        FileChosen?.Invoke(node);
    }

    [RelayCommand]
    private void NewFile() => NewFileRequested?.Invoke();

    private void AddHost(string alias) => HostAdded?.Invoke(alias);
}
