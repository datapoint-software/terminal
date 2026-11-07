using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Datapoint.Terminal.Editor;

// The line above a host's documentation: its environment, address and open terminals, with a way to open one.
public sealed partial class HostHeaderViewModel(string name, string? environment, string? address, Action openTerminal) : ObservableObject
{
    public string Name { get; } = name;

    public string? Environment { get; } = environment;

    public bool HasEnvironment => Environment is not null;

    public bool IsProduction => Agent.EnvironmentLabels.IsProduction(Environment);

    public string? Address { get; } = address;

    public bool HasAddress => !string.IsNullOrEmpty(Address);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOpenText))]
    public partial string OpenText { get; set; } = string.Empty;

    public bool HasOpenText => OpenText.Length > 0;

    [RelayCommand]
    private void OpenTerminal() => openTerminal();
}
