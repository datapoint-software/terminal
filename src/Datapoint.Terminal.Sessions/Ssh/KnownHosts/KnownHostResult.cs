namespace Datapoint.Terminal.Sessions.Ssh.KnownHosts;

public sealed record KnownHostResult(KnownHostStatus Status, IReadOnlyList<string> KnownKeyTypes, string? Source = null, int? Line = null);
