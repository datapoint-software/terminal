namespace Datapoint.Terminal.Sessions.Ssh.KnownHosts;

public sealed record HostKeyQuestion(string ConnectionName, string Host, int Port, string KeyType, string Fingerprint, IReadOnlyList<string> OtherKnownKeyTypes);
