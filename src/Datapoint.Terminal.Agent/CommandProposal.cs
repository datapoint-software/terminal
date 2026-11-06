using Datapoint.Terminal.Agent.Safety;

namespace Datapoint.Terminal.Agent;

public sealed record CommandProposal(string Command, string Purpose, CommandRisk Risk);
