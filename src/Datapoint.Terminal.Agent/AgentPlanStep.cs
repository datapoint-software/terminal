namespace Datapoint.Terminal.Agent;

// Warning is the agent's own note on what could go wrong, e.g. that a reboot ends the connection; the app adds its own.
public sealed record AgentPlanStep(string Command, string Purpose, string? Warning = null);
