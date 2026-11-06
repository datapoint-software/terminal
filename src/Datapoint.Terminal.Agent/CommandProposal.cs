using Datapoint.Terminal.Agent.Safety;

namespace Datapoint.Terminal.Agent;

/// <summary>A command the agent wants to type, with the reason it gave.</summary>
public sealed record CommandProposal(string Command, string Purpose, CommandRisk Risk);
