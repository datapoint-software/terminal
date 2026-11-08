using Datapoint.Terminal.Agent.Providers;

namespace Datapoint.Terminal.Setup;

public sealed record ProviderChoice(ProviderKind Kind, string Title, string Detail);
