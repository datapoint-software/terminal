using Avalonia.Threading;
using Datapoint.Terminal.Agent.Files;
using Datapoint.Terminal.Agent.Skills;
using Datapoint.Terminal.Agent.Workspaces;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Shell;

// Product decision 101: a workspace's instructions and skills say what the agent does, so a change from outside (a teammate,
// through a sync service, Git or SVN) waits for the user. The agent keeps the copy the user last accepted until then, and a
// skill that arrived from outside isn't used at all. Files the application writes itself are accepted as they're written.
public sealed partial class MainWindow
{
    private readonly HashSet<string> _reviewsShown = new(StringComparer.Ordinal);

    private string SkillsPath => Path.Combine(Workspace.Folder, "skills");

    private InstructionSnapshots Accepted => _windows.InstructionSnapshots;

    // The first time a trusted workspace is opened with this check, its skills are taken as they are, so only later changes
    // ask; a workspace not trusted yet gives its skills to the agent as data until then.
    private void TakeSkillBaseline()
    {
        if (Workspace.IsDefault || _trust is not { IsTrusted: true } || Accepted.IsKnown(SkillsPath, _redactor))
        {
            return;
        }

        foreach (var file in SkillFiles())
        {
            Accepted.Accept(file, TextFiles.Read(file), _redactor);
        }

        Accepted.Accept(SkillsPath, string.Empty, _redactor);
    }

    private IEnumerable<string> SkillFiles()
    {
        try
        {
            return Directory.Exists(SkillsPath)
                ? [.. Directory.EnumerateDirectories(SkillsPath).Select(folder => Path.Combine(folder, SkillDocument.FileName)).Where(File.Exists)]
                : [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    // Called from the agent's thread as well as the window's.
    private string? ReviewedSkill(string path, string text)
    {
        if (Workspace.IsDefault || !Accepted.IsKnown(SkillsPath, _redactor))
        {
            return text;
        }

        var status = Accepted.Check(path, text, _redactor, out var accepted);
        if (status != ReviewStatus.Accepted)
        {
            AskToReview(path, text, isNewSkill: status == ReviewStatus.New);
        }

        return status switch
        {
            ReviewStatus.Accepted => text,
            ReviewStatus.Changed => accepted!.Text,
            _ => null,
        };
    }

    // The workspace's own instruction files, once trusted as they were shown; the global file is the user's own, and a file
    // the workspace's trust doesn't cover is given as data anyway.
    private InstructionFile ReviewedInstruction(InstructionFile file, bool isCovered)
    {
        if (Workspace.IsDefault || !isCovered || file.Problem is not null)
        {
            return file;
        }

        switch (Accepted.Check(file.Path, file.Text, _redactor, out var accepted))
        {
            case ReviewStatus.New:
                Accepted.Accept(file.Path, file.Text, _redactor);
                return file;
            case ReviewStatus.Changed:
                AskToReview(file.Path, file.Text, isNewSkill: false);
                return file with { Text = accepted!.Text };
            default:
                return file;
        }
    }

    // Once per version of a file and window, as a notice the user can act on when it suits them.
    private void AskToReview(string path, string text, bool isNewSkill)
    {
        lock (_reviewsShown)
        {
            if (!_reviewsShown.Add(path + "\n" + Agent.Safety.SecretRedactor.Fingerprint(text)))
            {
                return;
            }
        }

        var shown = WorkspaceFolders.Display(path);
        var notice = isNewSkill ? Strings.Format(Strings.NoticeNewSkillOutside, shown) : Strings.Format(Strings.NoticeChangedOutside, shown);
        Dispatcher.UIThread.Post(() => _chat.AddNotice(notice, Strings.ReviewChangeAction, () => _ = ReviewChangeAsync(path)));
    }

    private async Task ReviewChangeAsync(string path)
    {
        var text = TextFiles.Read(path);
        var status = Accepted.Check(path, text, _redactor, out var accepted);
        if (status == ReviewStatus.Accepted)
        {
            return;
        }

        var isNew = status == ReviewStatus.New;
        var choice = await AskAboutChangeAsync(path, text, accepted).ConfigureAwait(true);
        if (choice == 1)
        {
            Accepted.Accept(path, text, _redactor);
            _chat.AddNotice(Strings.Format(Strings.NoticeChangeAccepted, WorkspaceFolders.Display(path)));
            _sidebar.RefreshSkills();
            return;
        }

        if (choice != 0 || isNew)
        {
            return;
        }

        KeepMine(path, accepted!);
    }

    // 0 keeps the user's version (or leaves a new skill out), 1 accepts the change, null decides nothing.
    private Task<int?> AskAboutChangeAsync(string path, string text, InstructionSnapshot? accepted)
    {
        var shown = WorkspaceFolders.Display(path);
        var isNew = accepted is null;
        var message = isNew ? Strings.ReviewNewSkillMessage : Strings.ReviewChangeMessage;
        if (!isNew && IsLooser(path, accepted!.Text, text))
        {
            message = Strings.ReviewLooser + Environment.NewLine + Environment.NewLine + message;
        }

        return ChoiceWindow.AskAsync(
            this,
            Strings.Format(isNew ? Strings.ReviewNewSkillTitle : Strings.ReviewChangeTitle, shown),
            message,
            LineDiff.Describe(accepted?.Text ?? string.Empty, _redactor.Redact(text)),
            hasDefault: false,
            isNew ? Strings.ReviewLeaveOut : Strings.ReviewKeepMine,
            isNew ? Strings.ReviewAcceptSkill : Strings.ReviewAcceptChange);
    }

    // The accepted copy is written back when it's whole; one with masked secrets would lose them, so it stays the agent's only.
    private void KeepMine(string path, InstructionSnapshot accepted)
    {
        var shown = WorkspaceFolders.Display(path);
        if (Workspace.IsReadOnly || !InstructionSnapshots.IsWhole(accepted))
        {
            _chat.AddNotice(Strings.Format(Strings.NoticeKeptForAgent, shown));
            return;
        }

        try
        {
            TextFiles.Write(path, accepted.Text, isPrivate: false);
            _chat.AddNotice(Strings.Format(Strings.NoticeChangeKept, shown));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _chat.AddNotice(exception.Message);
        }
    }

    // A skill whose rollout is less careful, or which lost a label and so reaches more hosts, is marked in the review.
    private static bool IsLooser(string path, string before, string after)
    {
        if (!string.Equals(Path.GetFileName(path), SkillDocument.FileName, StringComparison.Ordinal))
        {
            return false;
        }

        var old = SkillDocument.Read(before, path);
        var now = SkillDocument.Read(after, path);
        return now.Rollout.IsLooserThan(old.Rollout) || old.Labels.Any(label => !now.Labels.Contains(label, StringComparer.OrdinalIgnoreCase));
    }
}
