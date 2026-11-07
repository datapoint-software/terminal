using CommunityToolkit.Mvvm.Input;

namespace Datapoint.Terminal.Sidebar;

// A host from the user's SSH configuration that the workspace doesn't list yet.
public sealed partial class SshHostItemViewModel(string alias, Action<string> add)
{
    public string Alias { get; } = alias;

    [RelayCommand]
    private void Add() => add(Alias);
}
