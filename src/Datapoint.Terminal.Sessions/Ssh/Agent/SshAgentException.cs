namespace Datapoint.Terminal.Sessions.Ssh.Agent;

public sealed class SshAgentException : Exception
{
    public SshAgentException()
    {
    }

    public SshAgentException(string message)
        : base(message)
    {
    }

    public SshAgentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
