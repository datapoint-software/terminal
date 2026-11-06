using Datapoint.Terminal.Security.Cryptography;
using Datapoint.Terminal.Sessions.Ssh.Transport;

namespace Datapoint.Terminal.Sessions.Ssh;

public sealed record SshConnectionOptions
{
    public required string Host { get; init; }

    public int Port { get; init; } = 22;

    public required string UserName { get; init; }

    public required IHostKeyVerifier HostKeyVerifier { get; init; }

    public AlgorithmPreferences Algorithms { get; init; } = AlgorithmPreferences.Standard;

    public int MinimumRsaBits { get; init; } = RsaPublicKey.DefaultMinimumBits;

    public TimeSpan RekeyInterval { get; init; } = TimeSpan.FromHours(1);

    public long RekeyBytes { get; init; } = 1L << 30;

    public long RekeyPackets { get; init; } = 1L << 31;

    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public bool RefuseTrivialAuthentication { get; init; }
}
