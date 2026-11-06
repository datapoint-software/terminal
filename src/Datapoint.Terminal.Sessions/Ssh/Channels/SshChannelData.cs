namespace Datapoint.Terminal.Sessions.Ssh.Channels;

/// <summary>Output from a channel; <see cref="IsError"/> marks the standard error stream.</summary>
public sealed record SshChannelData(ReadOnlyMemory<byte> Data, bool IsError);
