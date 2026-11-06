namespace Datapoint.Terminal.Sessions.Ssh.Protocol;

public sealed class SshProtocolException : Exception
{
    public SshProtocolException()
    {
    }

    public SshProtocolException(string message)
        : base(message)
    {
    }

    public SshProtocolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public SshProtocolException(string message, DisconnectReason reason)
        : base(message)
    {
        Reason = reason;
    }

    public DisconnectReason Reason { get; } = DisconnectReason.ProtocolError;
}
