using System.Collections.ObjectModel;
using Datapoint.Terminal.Agent;

namespace Datapoint.Terminal.Chat;

// A host on the Looking card, with the commands the agent ran there.
public sealed class LookProgressHostViewModel(AgentLookHost host, bool isFirst)
{
    public HostChipViewModel Chip { get; } = new(host.Name, host.Environment);

    public bool IsFirst => isFirst;

    public ObservableCollection<LookCommandViewModel> Commands { get; } = [];

    // A terminal opened for a connection is named after it, with a number when there are several, e.g. "web1 2".
    public bool Owns(string terminal) =>
        string.Equals(terminal, host.Name, StringComparison.OrdinalIgnoreCase)
        || terminal.StartsWith(host.Name + " ", StringComparison.OrdinalIgnoreCase);
}
