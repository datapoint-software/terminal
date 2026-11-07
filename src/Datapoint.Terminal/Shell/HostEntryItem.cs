using Datapoint.Terminal.Agent.Files;

namespace Datapoint.Terminal.Shell;

// A folder shows with a slash after its name, as ls -p and shells' completion show it.
public sealed record HostEntryItem(HostEntry Entry)
{
    public string Display => Entry.IsFolder ? Entry.Name + "/" : Entry.Name;
}
