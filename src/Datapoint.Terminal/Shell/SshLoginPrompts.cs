using Avalonia.Controls;
using Avalonia.Threading;
using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Sessions.Ssh.Authentication;
using Datapoint.Terminal.Sessions.Ssh.KnownHosts;

namespace Datapoint.Terminal.Shell;

// Logging in happens in the application's own windows, never in the terminal or the chat (product decision 22). Secrets typed
// here are added to the redactor, so they're masked if a program ever prints them.
//
// A password can be kept in the credential store for the login (product decision 113): the user is asked before it's used,
// unless they chose to always allow it, and it's only ever given to the server the login names, after its host key checked.
public sealed class SshLoginPrompts(Window owner, string connectionName, SecretRedactor redactor, Action<string> notice, SavedLoginSource? saved = null) : IAuthenticationPrompts, IHostKeyPrompt
{
    private bool _triedSaved;
    private string? _toSave;

    // Called once logged in, so only a password the server accepted is kept.
    public void LoggedIn()
    {
        if (saved is null || _toSave is not { } password)
        {
            return;
        }

        _toSave = null;
        try
        {
            saved.Settings.SaveLoginPassword(saved.Login, password);
        }
        catch (Security.Credentials.CredentialStoreException exception)
        {
            notice(Strings.Format(Strings.NoticeLoginNotSaved, connectionName, exception.Message));
        }
    }

    public void ShowBanner(string text) => Dispatcher.UIThread.Post(() => notice(Strings.Format(Strings.NoticeServerBanner, connectionName, text)));

    public Task<string?> ReadPasswordAsync(string userName, string host, CancellationToken cancellationToken) =>
        OnUiAsync(() => PasswordAsync(userName, host));

    private async Task<string?> PasswordAsync(string userName, string host)
    {
        if (!_triedSaved && SavedPassword() is { } savedPassword && await MayUseSavedAsync(userName, host).ConfigureAwait(true))
        {
            _triedSaved = true;
            redactor.AddKnownSecret(savedPassword);
            return savedPassword;
        }

        var canSave = saved?.Settings.LoginPasswords.CanSave == true;
        var answer = await PromptWindow.AskPasswordAsync(
            owner,
            Strings.Format(Strings.PasswordTitle, connectionName),
            Strings.Format(_triedSaved ? Strings.PasswordSavedRefused : Strings.PasswordMessage, userName, host),
            Strings.PasswordField,
            canSave ? Strings.Format(Strings.PasswordRemember, saved!.Settings.LoginPasswords.StoreName) : null,
            remember: _triedSaved).ConfigureAwait(true);
        var password = answer?.Password;
        redactor.AddKnownSecret(password ?? string.Empty);
        _toSave = answer is { Remember: true } ? password : null;
        return password;
    }

    private string? SavedPassword()
    {
        try
        {
            return saved is not null && saved.Settings.SavedLoginFor(saved.Login) is not null ? saved.Settings.LoginPasswords.Read(saved.Login) : null;
        }
        catch (Security.Credentials.CredentialStoreException exception)
        {
            notice(Strings.Format(Strings.NoticeLoginNotRead, connectionName, exception.Message));
            return null;
        }
    }

    private async Task<bool> MayUseSavedAsync(string userName, string host)
    {
        if (saved!.Settings.SavedLoginFor(saved.Login)?.UsesWithoutAsking == true)
        {
            return true;
        }

        var choice = await ChoiceWindow.AskAsync(
            owner,
            Strings.Format(Strings.PasswordSavedTitle, connectionName),
            Strings.Format(Strings.PasswordSavedMessage, userName, host),
            details: null,
            hasDefault: false,
            Strings.PasswordSavedTypeIt,
            Strings.PasswordSavedOnce,
            Strings.PasswordSavedAlways).ConfigureAwait(true);
        if (choice == 2)
        {
            saved.Settings.SetUsesWithoutAsking(saved.Login, true);
        }

        return choice is 1 or 2;
    }

    public Task<IReadOnlyList<string>?> AnswerAsync(KeyboardInteractiveRequest request, CancellationToken cancellationToken) => OnUiAsync(async () =>
    {
        ArgumentNullException.ThrowIfNull(request);
        if (saved is not null && IsPasswordOnly(request))
        {
            return await PasswordAsync(saved.User, saved.Host).ConfigureAwait(true) is { } password ? [password] : null;
        }

        var serverText = string.Join("\n", new[] { request.Name, request.Instruction }.Where(text => !string.IsNullOrWhiteSpace(text)));
        var answers = await PromptWindow.AskAsync(
            owner,
            Strings.Format(Strings.KeyboardInteractiveTitle, connectionName),
            Strings.KeyboardInteractiveMessage,
            serverText,
            [.. request.Prompts.Select(prompt => (prompt.Text, !prompt.Echo))]).ConfigureAwait(true);
        for (var index = 0; answers is not null && index < answers.Count; index++)
        {
            if (!request.Prompts[index].Echo)
            {
                redactor.AddKnownSecret(answers[index]);
            }
        }

        return answers;
    });

    public Task<NewHostKeyChoice> AskAboutNewKeyAsync(HostKeyQuestion question, CancellationToken cancellationToken) => OnUiAsync(async () =>
    {
        ArgumentNullException.ThrowIfNull(question);
        var message = Strings.HostKeyNewMessage;
        if (question.OtherKnownKeyTypes.Count > 0)
        {
            message += "\n\n" + Strings.Format(Strings.HostKeyNewOtherTypes, string.Join(", ", question.OtherKnownKeyTypes));
        }

        var choice = await ChoiceWindow.AskAsync(
            owner,
            Strings.Format(Strings.HostKeyNewTitle, question.ConnectionName),
            message,
            Details(question),
            hasDefault: false,
            Strings.HostKeyCancel,
            Strings.HostKeyOnce,
            Strings.HostKeyTrust).ConfigureAwait(true);
        return choice switch
        {
            1 => NewHostKeyChoice.ConnectOnce,
            2 => NewHostKeyChoice.TrustAndConnect,
            _ => NewHostKeyChoice.Cancel,
        };
    });

    public Task ShowChangedKeyAsync(HostKeyQuestion question, KnownHostResult known, CancellationToken cancellationToken) => OnUiAsync(async () =>
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(known);
        await ChoiceWindow.AskAsync(
            owner,
            Strings.Format(Strings.HostKeyChangedTitle, question.ConnectionName),
            Strings.Format(Strings.HostKeyChangedMessage, known.Source, known.Line, question.Host),
            Details(question),
            hasDefault: true,
            Strings.Close).ConfigureAwait(true);
        return true;
    });

    public Task ShowRevokedKeyAsync(HostKeyQuestion question, KnownHostResult known, CancellationToken cancellationToken) => OnUiAsync(async () =>
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(known);
        await ChoiceWindow.AskAsync(
            owner,
            Strings.Format(Strings.HostKeyRevokedTitle, question.ConnectionName),
            Strings.Format(Strings.HostKeyRevokedMessage, known.Source, known.Line),
            Details(question),
            hasDefault: true,
            Strings.Close).ConfigureAwait(true);
        return true;
    });

    // Many servers ask for the login password this way rather than with the password method.
    private static bool IsPasswordOnly(KeyboardInteractiveRequest request) =>
        request.Prompts.Count == 1 && !request.Prompts[0].Echo && request.Prompts[0].Text.Contains("password", StringComparison.OrdinalIgnoreCase);

    private static string Details(HostKeyQuestion question) =>
        Strings.Format(Strings.HostKeyDetails, question.Port == 22 ? question.Host : $"{question.Host}:{question.Port}", question.KeyType, question.Fingerprint);

    private static Task<T> OnUiAsync<T>(Func<Task<T>> action) => Dispatcher.UIThread.InvokeAsync(action);
}
