namespace Datapoint.Terminal.Agent.Projects.History;

public sealed class HistoryException : Exception
{
    public HistoryException()
    {
    }

    public HistoryException(string message)
        : base(message)
    {
    }

    public HistoryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
