using Datapoint.Terminal.Agent.Providers;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Setup;

// A provider to start with, described in the interface's language (product decision 150).
public sealed class ProviderChoice(ProviderKind kind, Func<string> title, Func<string> detail) : LocalizedObject
{
    public ProviderKind Kind { get; } = kind;

    public string Title => title();

    public string Detail => detail();
}
