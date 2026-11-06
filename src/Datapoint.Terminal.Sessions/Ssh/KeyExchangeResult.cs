using Datapoint.Terminal.Sessions.Ssh.Keys;
using Datapoint.Terminal.Sessions.Ssh.Transport;

namespace Datapoint.Terminal.Sessions.Ssh;

public sealed record KeyExchangeResult(NegotiatedAlgorithms Algorithms, SshPublicKey HostKey, byte[] SessionId);
