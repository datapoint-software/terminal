namespace Datapoint.Terminal.Sessions.Ssh.Authentication;

public sealed class SshAuthenticationException : Exception
{
    public SshAuthenticationException()
    {
    }

    public SshAuthenticationException(string message)
        : base(message)
    {
    }

    public SshAuthenticationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
