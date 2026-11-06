namespace Datapoint.Terminal.Sessions.Ssh.Config;

internal sealed record SshConfigLine(string Keyword, IReadOnlyList<string> Arguments, string Source, int Number);
