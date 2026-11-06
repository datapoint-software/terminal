using Datapoint.Terminal.Sessions.Ssh.Protocol;

namespace Datapoint.Terminal.Sessions.Ssh;

public sealed class SshDisconnectedException : Exception
{
    public SshDisconnectedException()
    {
    }

    public SshDisconnectedException(string message)
        : base(message)
    {
    }

    public SshDisconnectedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public SshDisconnectedException(DisconnectReason reason, string description)
        : base(description)
    {
        Reason = reason;
    }

    public DisconnectReason Reason { get; }
}
