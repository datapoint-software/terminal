namespace Datapoint.Terminal.ThirdPartyNotices;

public sealed class NoticeException : Exception
{
    public NoticeException()
    {
    }

    public NoticeException(string message)
        : base(message)
    {
    }

    public NoticeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
