namespace Datapoint.Terminal.Sessions.Ssh.Authentication;

/// <summary>
/// Asks the user for what a login needs. Every text from the server has been sanitized already;
/// it is still untrusted and must be shown as the server's, never as the application's.
/// </summary>
public interface IAuthenticationPrompts
{
    /// <summary>Shows a banner the server sent before login.</summary>
    void ShowBanner(string text);

    /// <summary>Asks for the password; null means the user declined.</summary>
    Task<string?> ReadPasswordAsync(string userName, string host, CancellationToken cancellationToken);

    /// <summary>Answers a keyboard-interactive round; null means the user declined.</summary>
    Task<IReadOnlyList<string>?> AnswerAsync(KeyboardInteractiveRequest request, CancellationToken cancellationToken);
}
