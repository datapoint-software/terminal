namespace Datapoint.Terminal.Sessions.Ssh.Authentication;

// Every text from the server has been sanitized already; it is still untrusted and must be shown as the server's,
// never as the application's.
public interface IAuthenticationPrompts
{
    void ShowBanner(string text);

    Task<string?> ReadPasswordAsync(string userName, string host, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>?> AnswerAsync(KeyboardInteractiveRequest request, CancellationToken cancellationToken);
}
