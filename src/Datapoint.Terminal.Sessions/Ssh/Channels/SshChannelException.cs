namespace Datapoint.Terminal.Sessions.Ssh.Channels;

public sealed class SshChannelException : Exception
{
    public SshChannelException()
    {
    }

    public SshChannelException(string message)
        : base(message)
    {
    }

    public SshChannelException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
