namespace Datapoint.Terminal.Sessions.Ssh;

// An upload that would have replaced a file, so the user can be asked first.
public sealed class RemoteFileExistsException : IOException
{
    public RemoteFileExistsException()
        : base("A file with that name is already there.")
    {
    }

    public RemoteFileExistsException(string message)
        : base(message)
    {
    }

    public RemoteFileExistsException(string message, int hresult)
        : base(message, hresult)
    {
    }

    public RemoteFileExistsException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
