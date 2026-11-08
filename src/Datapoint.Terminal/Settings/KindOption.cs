using Datapoint.Terminal.Agent.Providers;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Settings;

// A kind of provider, named in the interface's language (product decision 150). Settings keeps one of each, so a
// provider's kind stays the same object when the language changes.
public sealed class KindOption(ProviderKind kind, Func<string> label) : LocalizedObject
{
    public ProviderKind Kind { get; } = kind;

    public string Label => label();

    public override string ToString() => Label;
}
