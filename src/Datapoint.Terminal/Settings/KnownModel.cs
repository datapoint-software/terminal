using Datapoint.Terminal.Agent.Providers;
using Datapoint.Terminal.Agent.Usage;

namespace Datapoint.Terminal.Settings;

// A model the app knows: its context window in tokens and what it costs, as its provider publishes them.
public sealed record KnownModel(ProviderKind Kind, string Id, string Name, int ContextWindow, TokenPrice Price);
