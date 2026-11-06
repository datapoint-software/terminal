using Avalonia.Controls;
using Avalonia.Threading;
using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Sessions.Ssh.Authentication;
using Datapoint.Terminal.Sessions.Ssh.KnownHosts;

namespace Datapoint.Terminal.Shell;

// Logging in happens in the application's own windows, never in the terminal or the chat (product decision 22). Secrets typed
// here are added to the redactor, so they're masked if a program ever prints them.
public sealed class SshLoginPrompts(Window owner, string connectionName, SecretRedactor redactor, Action<string> notice) : IAuthenticationPrompts, IHostKeyPrompt
{
    public void ShowBanner(string text) => Dispatcher.UIThread.Post(() => notice(Strings.Format(Strings.NoticeServerBanner, connectionName, text)));

    public Task<string?> ReadPasswordAsync(string userName, string host, CancellationToken cancellationToken) => OnUiAsync(async () =>
    {
        var answers = await PromptWindow.AskAsync(
            owner,
            Strings.Format(Strings.PasswordTitle, connectionName),
            Strings.Format(Strings.PasswordMessage, userName, host),
            serverText: null,
            [(Strings.PasswordField, true)]).ConfigureAwait(true);
        var password = answers?[0];
        redactor.AddKnownSecret(password ?? string.Empty);
        return password;
    });

    public Task<IReadOnlyList<string>?> AnswerAsync(KeyboardInteractiveRequest request, CancellationToken cancellationToken) => OnUiAsync(async () =>
    {
        ArgumentNullException.ThrowIfNull(request);
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

    private static string Details(HostKeyQuestion question) =>
        Strings.Format(Strings.HostKeyDetails, question.Port == 22 ? question.Host : $"{question.Host}:{question.Port}", question.KeyType, question.Fingerprint);

    private static Task<T> OnUiAsync<T>(Func<Task<T>> action) => Dispatcher.UIThread.InvokeAsync(action);
}
