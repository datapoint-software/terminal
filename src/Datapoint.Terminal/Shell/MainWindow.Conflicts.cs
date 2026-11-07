using Avalonia.Threading;
using Datapoint.Terminal.Agent.Files;
using Datapoint.Terminal.Agent.Workspaces;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Shell;

// Product decision 101: when a sync service leaves two versions of an instruction file or a skill, the agent reads neither,
// and the user settles them as one item: keep one, or both changes when they touch different lines. The version not kept is
// moved out of the workspace into the application's data, never deleted.
public sealed partial class MainWindow
{
    private static string ConflictsFolder => Path.Combine(Path.GetDirectoryName(WorkspaceFolders.PersonalRoot)!, "conflicts");

    // True when the file has a conflict copy, after offering to settle it; called from the agent's thread too.
    private bool HasConflict(string path)
    {
        if (ConflictCopies.Find(path) is not { Count: > 0 } copies)
        {
            return false;
        }

        lock (_reviewsShown)
        {
            if (!_reviewsShown.Add("conflict\n" + path + "\n" + copies[0]))
            {
                return true;
            }
        }

        var notice = Strings.Format(Strings.NoticeConflict, WorkspaceFolders.Display(path));
        Dispatcher.UIThread.Post(() => _chat.AddNotice(notice, Strings.ConflictAction, () => _ = SettleAsync(path)));
        return true;
    }

    private async Task SettleAsync(string path)
    {
        if (ConflictCopies.Find(path) is not { Count: > 0 } copies)
        {
            return;
        }

        var copy = copies[0];
        var (mine, theirs) = (TextFiles.Read(path), TextFiles.Read(copy));
        var merged = BothChanges(path, mine, theirs);
        var (name, copyName) = (Path.GetFileName(path), Path.GetFileName(copy));
        string[] choices = merged is null
            ? [Strings.Format(Strings.ConflictKeep, name), Strings.Format(Strings.ConflictKeep, copyName)]
            : [Strings.Format(Strings.ConflictKeep, name), Strings.Format(Strings.ConflictKeep, copyName), Strings.ConflictKeepBoth];
        var details = Strings.Format(Strings.ConflictDetails, name, copyName) + Environment.NewLine + LineDiff.Describe(_redactor.Redact(mine), _redactor.Redact(theirs));
        var choice = await ChoiceWindow.AskAsync(this, Strings.Format(Strings.ConflictTitle, WorkspaceFolders.Display(path)), Strings.ConflictMessage, details, hasDefault: false, choices).ConfigureAwait(true);
        var kept = choice switch
        {
            0 => mine,
            1 => theirs,
            2 => merged,
            _ => null,
        };
        if (kept is null)
        {
            return;
        }

        Settle(path, copy, kept);
    }

    // Both versions changed the copy the user last accepted; when that copy is whole and they changed different lines, both
    // changes can be kept.
    private string? BothChanges(string path, string mine, string theirs) =>
        Accepted.Check(path, mine, _redactor, out var accepted) != ReviewStatus.New && accepted is not null && InstructionSnapshots.IsWhole(accepted)
            ? LineMerge.TryMerge(accepted.Text, mine, theirs)
            : null;

    private void Settle(string path, string copy, string kept)
    {
        try
        {
            var aside = Path.Combine(ConflictsFolder, DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture), Path.GetFileName(copy));
            Directory.CreateDirectory(Path.GetDirectoryName(aside)!);
            File.Copy(copy, aside);
            TextFiles.Write(path, kept, isPrivate: false);
            File.Delete(copy);
            _chat.AddNotice(Strings.Format(Strings.NoticeConflictSettled, WorkspaceFolders.Display(path), aside));
            _sidebar.RefreshSkills();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _chat.AddNotice(exception.Message);
        }
    }
}
