using System.Buffers.Binary;
using System.IO.Pipes;
using System.Net.Sockets;
using Datapoint.Terminal.Sessions.Ssh.Protocol;
using Datapoint.Terminal.Sessions.Ssh.Transport;

namespace Datapoint.Terminal.Sessions.Ssh.Agent;

// Talks to the user's SSH agent, which keeps private keys (and hardware keys) out of this process: we only ever see public
// keys and signatures. Replies are bounded before anything is allocated.
public sealed class SshAgentClient(string address)
{
    private const byte Failure = 5;
    private const byte RequestIdentities = 11;
    private const byte IdentitiesAnswer = 12;
    private const byte SignRequest = 13;
    private const byte SignResponse = 14;
    private const uint RsaSha256Flag = 2;
    private const uint RsaSha512Flag = 4;
    private const int MaxReply = 256 * 1024;
    private const int MaxIdentities = 1024;

    public string Address { get; } = address;

    // IdentityAgent from ~/.ssh/config wins; "none" turns the agent off, and "SSH_AUTH_SOCK" means the environment's.
    public static SshAgentClient? Find(string? identityAgent)
    {
        if (string.Equals(identityAgent, "none", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var address = identityAgent is null || string.Equals(identityAgent, "SSH_AUTH_SOCK", StringComparison.Ordinal)
            ? Environment.GetEnvironmentVariable("SSH_AUTH_SOCK")
            : identityAgent.StartsWith('$') ? Environment.GetEnvironmentVariable(identityAgent[1..]) : identityAgent;
        if (string.IsNullOrEmpty(address) && OperatingSystem.IsWindows())
        {
            address = @"\\.\pipe\openssh-ssh-agent";
        }

        return string.IsNullOrEmpty(address) ? null : new SshAgentClient(address);
    }

    public async Task<IReadOnlyList<SshAgentSigner>> ListKeysAsync(CancellationToken cancellationToken)
    {
        var reply = await RequestAsync([RequestIdentities], cancellationToken).ConfigureAwait(false);
        if (reply.Length == 0 || reply[0] != IdentitiesAnswer)
        {
            return [];
        }

        return ReadIdentities(reply, this);
    }

    internal async Task<byte[]> SignAsync(ReadOnlyMemory<byte> keyBlob, ReadOnlyMemory<byte> data, string algorithm, CancellationToken cancellationToken)
    {
        var flags = algorithm switch
        {
            AlgorithmNames.RsaSha256 => RsaSha256Flag,
            AlgorithmNames.RsaSha512 => RsaSha512Flag,
            _ => 0u,
        };
        var request = new SshWriter().WriteByte(SignRequest).WriteString(keyBlob.Span).WriteString(data.Span).WriteUInt32(flags).ToArray();
        var reply = await RequestAsync(request, cancellationToken).ConfigureAwait(false);
        if (reply.Length == 0 || reply[0] != SignResponse)
        {
            throw new SshAgentException(reply.Length > 0 && reply[0] == Failure
                ? "The SSH agent refused to sign. It may need you to confirm, or the key may have been removed."
                : "The SSH agent sent an unexpected reply.");
        }

        return ReadSignature(reply);
    }

    private static List<SshAgentSigner> ReadIdentities(byte[] reply, SshAgentClient agent)
    {
        var reader = new SshReader(reply.AsSpan(1));
        var count = reader.ReadUInt32();
        if (count > MaxIdentities)
        {
            throw new SshAgentException("The SSH agent listed too many keys.");
        }

        var keys = new List<SshAgentSigner>((int)count);
        for (var index = 0; index < count; index++)
        {
            var blob = reader.ReadString().ToArray();
            var comment = reader.ReadUtf8String();
            var keyType = new SshReader(blob).ReadAsciiString();
            keys.Add(new SshAgentSigner(agent, keyType, blob, comment));
        }

        return keys;
    }

    private static byte[] ReadSignature(byte[] reply)
    {
        var reader = new SshReader(reply.AsSpan(1));
        return reader.ReadString().ToArray();
    }

    private async Task<byte[]> RequestAsync(byte[] message, CancellationToken cancellationToken)
    {
        var stream = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var disposeStream = stream.ConfigureAwait(false);
        var header = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(header, (uint)message.Length);
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(message, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadUInt32BigEndian(header);
        if (length is 0 or > MaxReply)
        {
            throw new SshAgentException("The SSH agent's reply was too large.");
        }

        var reply = new byte[length];
        await stream.ReadExactlyAsync(reply, cancellationToken).ConfigureAwait(false);
        return reply;
    }

    private async Task<Stream> OpenAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (Address.StartsWith(@"\\.\pipe\", StringComparison.OrdinalIgnoreCase))
            {
                var pipe = new NamedPipeClientStream(".", Address[@"\\.\pipe\".Length..], PipeDirection.InOut, PipeOptions.Asynchronous);
                await pipe.ConnectAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
                return pipe;
            }

            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            try
            {
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(Address), cancellationToken).ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
        catch (Exception exception) when (exception is SocketException or IOException or TimeoutException or UnauthorizedAccessException)
        {
            throw new SshAgentException($"The SSH agent at {Address} couldn't be reached: {exception.Message}", exception);
        }
    }
}
