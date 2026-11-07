namespace Datapoint.Terminal.Sessions.Ssh;

// Output past the limit is dropped and IsTruncated set; the command still runs to the end.
public sealed record SshCommandResult(byte[] Output, string Error, int? ExitCode, bool IsTruncated);
