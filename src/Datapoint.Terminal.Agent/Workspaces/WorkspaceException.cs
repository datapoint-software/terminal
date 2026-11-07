namespace Datapoint.Terminal.Agent.Workspaces;

public sealed class WorkspaceException : Exception
{
    public WorkspaceException()
    {
    }

    public WorkspaceException(string message)
        : base(message)
    {
    }

    public WorkspaceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
