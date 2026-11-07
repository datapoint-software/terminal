using Datapoint.Terminal.Agent.Workspaces;
using Datapoint.Terminal.Agent.Workspaces.Git;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Shell;

public sealed partial class MainWindow
{
    // Product decision 89. Git is never run: the ignore rules and the index are read from the repository's files.
    private async Task OfferGitIgnoreAsync()
    {
        if (Workspace.IsReadOnly || GitRepository.Find(Workspace.Folder, WorkspaceFolders.Home) is not { } repository)
        {
            return;
        }

        var personal = Workspace.PersonalPath;
        var isTracked = repository.HasTrackedFilesUnder(personal) == true;
        if (IsIgnored(repository, personal))
        {
            if (isTracked)
            {
                _chat.AddNotice(Strings.NoticeGitTracked);
            }

            return;
        }

        if (_settings.HasDeclinedGitIgnore(Workspace.Folder))
        {
            return;
        }

        var choice = await ChoiceWindow.AskAsync(
            this,
            Strings.GitIgnoreTitle,
            Strings.Format(Strings.GitIgnoreMessage, Workspace.Name, WorkspaceFolders.Display(repository.Root)),
            isTracked ? Strings.GitIgnoreTracked : null,
            hasDefault: true,
            Strings.GitIgnoreDont,
            Strings.GitIgnoreAdd).ConfigureAwait(true);
        if (choice == 0)
        {
            _settings.DeclineGitIgnore(Workspace.Folder);
        }

        if (choice != 1)
        {
            return;
        }

        AddToGitIgnore(repository, personal);
    }

    private void AddToGitIgnore(GitRepository repository, string personal)
    {
        try
        {
            var path = WorkspaceFolders.Display(repository.AddToGitIgnore(personal));
            _chat.AddNotice(Strings.Format(IsIgnored(repository, personal) ? Strings.NoticeGitIgnoreAdded : Strings.NoticeGitIgnoreStillNot, path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _chat.AddNotice(Strings.Format(Strings.NoticeGitIgnoreFailed, exception.Message));
        }
    }

    private static bool IsIgnored(GitRepository repository, string personal) =>
        repository.IgnoreRulesFor(personal, WorkspaceFolders.Home).IsIgnored(personal, isDirectory: true);
}
