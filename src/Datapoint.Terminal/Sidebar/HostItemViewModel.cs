using CommunityToolkit.Mvvm.ComponentModel;
using Datapoint.Terminal.Agent.Workspaces;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Sidebar;

// One of the workspace's hosts, with a small mark for its environment: red for production, amber for staging, the accent for
// any other label, and none without one. Screen readers hear the label instead.
public sealed partial class HostItemViewModel(WorkspaceConnection connection) : ObservableObject
{
    public WorkspaceConnection Connection { get; } = connection;

    public string Name => Connection.Name;

    public string AccessibleName => Connection.Environment is { } environment ? $"{Name}, {environment}" : Name;

    public bool IsProduction => Connection.IsProduction;

    public bool IsStaging => !IsProduction && Connection.Environment?.StartsWith("stag", StringComparison.OrdinalIgnoreCase) == true;

    public bool IsOtherEnvironment => Connection.Environment is not null && !IsProduction && !IsStaging;

    public bool HasNoEnvironment => Connection.Environment is null;

    public string? Environment => Connection.Environment;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen), nameof(OpenText))]
    public partial int OpenTerminals { get; set; }

    public bool IsOpen => OpenTerminals > 0;

    public string OpenText => IsOpen ? Strings.Format(Strings.SidebarOpenTerminals, OpenTerminals) : string.Empty;
}
