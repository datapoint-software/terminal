namespace Datapoint.Terminal.Sessions.Ssh.Channels;

public sealed record SshChannelData(ReadOnlyMemory<byte> Data, bool IsError);
