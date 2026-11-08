using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Datapoint.Terminal.Agent;

namespace Datapoint.Terminal.Chat;

// A host on the Looking card, with the commands the agent ran there, and, until the first one, how its terminal is coming
// along (opening, connecting) in place of a line each in the chat.
public sealed partial class LookProgressHostViewModel(AgentLookHost host, bool isFirst) : ObservableObject
{
    [ObservableProperty]
    public partial string? State { get; set; }

    public HostChipViewModel Chip { get; } = new(host.Name, host.Environment);

    public bool IsFirst => isFirst;

    public ObservableCollection<LookCommandViewModel> Commands { get; } = [];

    // A terminal opened for a connection is named after it, with a number when there are several, e.g. "web1 2".
    public bool Owns(string terminal) =>
        string.Equals(terminal, host.Name, StringComparison.OrdinalIgnoreCase)
        || terminal.StartsWith(host.Name + " ", StringComparison.OrdinalIgnoreCase);
}
