using System.Globalization;
using Avalonia.Threading;
using Datapoint.Terminal.Agent.Projects;
using Datapoint.Terminal.Agent.Projects.HostKeys;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Sessions.Ssh;

namespace Datapoint.Terminal.Shell;

// Product decision 93: the project remembers the host key it last saw for each connection, and a different one, even if
// known_hosts accepts it, is a reason to ask whether this project's documentation for the machine still holds.
public sealed partial class MainWindow
{
    private ProjectHostKeys? _hostKeys;

    private ProjectHostKeys HostKeys => _hostKeys ??= new ProjectHostKeys(Project.IsReadOnly ? null : Path.Combine(Project.PersonalPath, ProjectHostKeys.FileName));

    private ProjectHostKeyVerifier ForProject(IHostKeyVerifier verifier, string connection) =>
        new(verifier, HostKeys, connection, (change, _) => Dispatcher.UIThread.InvokeAsync(() => AskAboutChangedKeyAsync(change)));

    private async Task<bool> AskAboutChangedKeyAsync(HostKeyChange change)
    {
        var docs = CreateHostDocs();
        var file = docs.FileFor(change.Connection);
        if (!File.Exists(file))
        {
            _chat.AddNotice(Strings.Format(Strings.NoticeProjectHostKeyChanged, change.Connection));
            return true;
        }

        var host = change.Port == 22 ? change.Host : $"{change.Host}:{change.Port}";
        var choices = docs.CanWrite
            ? new[] { Strings.Cancel, Strings.ProjectHostKeyArchive, Strings.ProjectHostKeyReview, Strings.ProjectHostKeyKeep }
            : [Strings.Cancel, Strings.ProjectHostKeyReview, Strings.ProjectHostKeyKeep];
        var answer = await ChoiceWindow.AskAsync(
            this,
            Strings.Format(Strings.ProjectHostKeyTitle, change.Connection),
            Strings.Format(Strings.ProjectHostKeyMessage, change.Connection, host),
            Strings.Format(Strings.ProjectHostKeyDetails, change.Before, change.Now),
            hasDefault: false,
            choices).ConfigureAwait(true);
        var chosen = answer is { } index ? choices[index] : Strings.Cancel;
        if (string.Equals(chosen, Strings.ProjectHostKeyArchive, StringComparison.Ordinal))
        {
            SetDocumentationAside(change.Connection, file);
        }
        else if (string.Equals(chosen, Strings.ProjectHostKeyReview, StringComparison.Ordinal))
        {
            OpenProjectFile(file);
        }

        return !string.Equals(chosen, Strings.Cancel, StringComparison.Ordinal);
    }

    // Moved, not deleted: the old notes may still help, e.g. to compare what was installed before.
    private void SetDocumentationAside(string connection, string file)
    {
        try
        {
            var folder = Path.Combine(Path.GetDirectoryName(file)!, "archive");
            Directory.CreateDirectory(folder);
            var stem = Path.GetFileNameWithoutExtension(file) + "-" + DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var target = Path.Combine(folder, stem + ".md");
            for (var number = 2; File.Exists(target); number++)
            {
                target = Path.Combine(folder, string.Create(CultureInfo.InvariantCulture, $"{stem}-{number}.md"));
            }

            File.Move(file, target);
            _chat.AddNotice(Strings.Format(Strings.NoticeHostDocsArchived, connection, ProjectFolders.RelativeInside(Project.Folder, target) ?? target));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _chat.AddNotice(Strings.Format(Strings.NoticeHostDocsArchiveFailed, connection, exception.Message));
        }
    }
}
