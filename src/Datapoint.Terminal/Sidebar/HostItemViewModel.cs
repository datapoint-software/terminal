using CommunityToolkit.Mvvm.ComponentModel;
using Datapoint.Terminal.Agent.Projects;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Sidebar;

// One of the project's hosts. Its environment shows on its document, not here, so the list stays calm.
public sealed partial class HostItemViewModel(ProjectConnection connection) : ObservableObject
{
    public ProjectConnection Connection { get; } = connection;

    public string Name => Connection.Name;

    public string AccessibleName => Connection.Environment is { } environment ? $"{Name}, {environment}" : Name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen), nameof(OpenText))]
    public partial int OpenTerminals { get; set; }

    public bool IsOpen => OpenTerminals > 0;

    public string OpenText => IsOpen ? Strings.Format(Strings.SidebarOpenTerminals, OpenTerminals) : string.Empty;
}
