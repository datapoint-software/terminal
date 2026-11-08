using System.Text;
using Avalonia.Threading;
using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Agent.Memory;
using Datapoint.Terminal.Agent.Workspaces;
using Datapoint.Terminal.Agent.Workspaces.Ignore;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Shell;

public sealed partial class MainWindow
{
    // Read on the agent's thread when a conversation starts.
    private volatile WorkspaceTrustRecord? _trust;

    private IReadOnlyList<string> WorkspaceInstructionFiles(string? globalFile) =>
        InstructionFiles.FindForWorkspace(Workspace.Folder, globalFile);

    // Asks when the workspace has something to trust and no decision covers it yet; from the menu, it always asks.
    private async Task ReviewTrustAsync(bool always)
    {
        var files = WorkspaceInstructionFiles(InstructionFiles.FindGlobal(WorkspaceFolders.Global));
        var record = _settings.TrustFor(Workspace.Folder);
        var state = WorkspaceTrust.Evaluate(Workspace.IsDefault, Workspace.Settings.Connections.Count > 0, files, record);
        if (state == TrustState.Trusted && record is null && !Workspace.IsDefault)
        {
            record = new WorkspaceTrustRecord { Folder = Workspace.Folder, IsTrusted = true, InstructionFiles = files };
            _settings.SetTrust(record);
        }

        if (state == TrustState.Ask || (always && !Workspace.IsDefault))
        {
            var choice = await ChoiceWindow.AskAsync(
                this,
                Strings.Format(Strings.TrustTitle, Workspace.Name),
                Strings.TrustMessage,
                TrustDetails(files, record),
                hasDefault: false,
                Strings.TrustDont,
                Strings.TrustTrust).ConfigureAwait(true);

            // Closing the question decides nothing: the workspace stays as it was, and is asked about again next time.
            if (choice is { } answer)
            {
                record = new WorkspaceTrustRecord { Folder = Workspace.Folder, IsTrusted = answer == 1, InstructionFiles = files };
                _settings.SetTrust(record);
            }
        }

        _trust = record;
        TakeSkillBaseline();
    }

    private string TrustDetails(IReadOnlyList<string> files, WorkspaceTrustRecord? record)
    {
        var connections = Workspace.Settings.Connections;
        if (files.Count == 0 && connections.Count == 0)
        {
            return Strings.TrustNothing;
        }

        var text = new StringBuilder();
        if (files.Count > 0)
        {
            text.AppendLine(Strings.TrustFiles);
            foreach (var file in files)
            {
                text.Append("  ").AppendLine(DescribeFile(file, record));
            }
        }

        if (connections.Count > 0)
        {
            text.AppendLine(files.Count > 0 ? Environment.NewLine + Strings.TrustConnections : Strings.TrustConnections);
            foreach (var connection in connections)
            {
                text.Append("  ").AppendLine(DescribeConnection(connection));
            }
        }

        return text.ToString().TrimEnd();
    }

    private static string DescribeFile(string file, WorkspaceTrustRecord? record)
    {
        var shown = WorkspaceFolders.Display(file);
        return record is not null && !record.Covers(file) ? Strings.Format(Strings.TrustNew, shown) : shown;
    }

    private static string DescribeConnection(WorkspaceConnection connection)
    {
        var target = ConnectionReview.Address(connection);
        var environment = connection.Environment is null ? string.Empty : $" ({connection.Environment})";
        return $"{connection.Name} → {target}{environment}";
    }

    // The global file is the user's own; the workspace's files are instructions only when the user trusted them as they were shown.
    // Files listed in .terminalignore aren't read at all, and those that look like they hold a secret wait for the user's choice.
    private async Task<AgentInstructions> LoadInstructionsAsync(CancellationToken cancellationToken)
    {
        var global = InstructionFiles.FindGlobal(WorkspaceFolders.Global);
        List<string> paths = [.. global is null ? [] : new[] { global }, .. WorkspaceInstructionFiles(global)];
        var ignored = paths.Where(path => TerminalIgnore.IsIgnored(path, Workspace.Folder, WorkspaceFolders.Home)).ToList();
        var trust = _trust;
        var read = InstructionFiles.Read([.. paths.Except(ignored, WorkspaceFolders.Comparer)]);
        var isOwn = (InstructionFile file) => global is not null && WorkspaceFolders.AreSame(file.Path, global);
        var changes = _windows.InstructionSnapshots.Update(read.Where(isOwn), _redactor);
        var files = WorkspaceInstructionsToUse(read, isOwn, trust);
        var (reviewed, skipped) = await ReviewSecretsAsync(files, cancellationToken).ConfigureAwait(false);
        var (memory, ignoredMemory, skippedMemory) = await LoadMemoryAsync(cancellationToken).ConfigureAwait(false);
        ignored.AddRange(ignoredMemory);
        skipped.AddRange(skippedMemory);
        var isTrusted = (InstructionFile file) => Workspace.IsDefault
            || isOwn(file)
            || WorkspaceFolders.AreSame(file.Path, GlobalMemoryPath)
            || (trust is { IsTrusted: true } && trust.Covers(file.Path));
        var instructions = new AgentInstructions(
            [.. reviewed.Where(isTrusted)],
            [.. memory.Where(file => !isTrusted(file)), .. reviewed.Where(file => !isTrusted(file))])
        {
            Ignored = TerminalIgnore.Describe(Workspace.Folder, WorkspaceFolders.Home),
            Memory = [.. memory.Where(isTrusted)],
        };
        Dispatcher.UIThread.Post(() =>
        {
            ReportNotSent(Strings.NoticeTerminalIgnored, ignored);
            ReportNotSent(Strings.NoticeFilesSkipped, skipped);
            ReportInstructions(instructions);
            ReportChanges(changes);
        });
        return instructions;
    }

    // The user's own file as it is; the workspace's as reviewed, and neither version of one a sync service left two of.
    private List<InstructionFile> WorkspaceInstructionsToUse(IEnumerable<InstructionFile> read, Func<InstructionFile, bool> isOwn, WorkspaceTrustRecord? trust) =>
        [.. read.Where(file => isOwn(file) || !HasConflict(file.Path))
            .Select(file => isOwn(file) ? file : ReviewedInstruction(file, trust is { IsTrusted: true } && trust.Covers(file.Path)))];

    private static string GlobalMemoryPath => MemoryLocation.Global;

    private string? WorkspaceMemoryPath => Workspace.IsDefault ? null : Path.Combine(Workspace.PersonalPath, MemoryFile.FileName);

    // Memory goes through the same checks as instruction files, but not their change notices: the agent changes it, in view.
    private async Task<(List<InstructionFile> Memory, List<string> Ignored, List<string> Skipped)> LoadMemoryAsync(CancellationToken cancellationToken)
    {
        var paths = new[] { GlobalMemoryPath, WorkspaceMemoryPath }.OfType<string>().Where(File.Exists).ToList();
        var ignored = paths.Where(path => TerminalIgnore.IsIgnored(path, Workspace.Folder, WorkspaceFolders.Home)).ToList();
        var files = InstructionFiles.Read([.. paths.Except(ignored, WorkspaceFolders.Comparer)], 2 * MemoryFile.MaxBytes);
        var (reviewed, skipped) = await ReviewSecretsAsync(files, cancellationToken).ConfigureAwait(false);
        return (reviewed, ignored, skipped);
    }

    private void ReportNotSent(string format, List<string> paths)
    {
        if (paths.Count == 0)
        {
            return;
        }

        _chat.AddNotice(Strings.Format(format, string.Join(", ", paths.Select(WorkspaceFolders.Display))));
    }

    private void ReportInstructions(AgentInstructions instructions)
    {
        static string Names(IEnumerable<InstructionFile> files) => string.Join(", ", files.Select(file => WorkspaceFolders.Display(file.Path)));
        var all = instructions.Instructions.Concat(instructions.Data).Concat(instructions.Memory).ToList();
        if (instructions.Memory.Any(file => file.Text.Length > 0))
        {
            _chat.AddNotice(Strings.Format(Strings.NoticeMemory, Names(instructions.Memory.Where(file => file.Text.Length > 0))));
        }

        if (instructions.Instructions.Any(file => file.Text.Length > 0))
        {
            _chat.AddNotice(Strings.Format(Strings.NoticeInstructions, Names(instructions.Instructions.Where(file => file.Text.Length > 0))));
        }

        if (instructions.Data.Any(file => file.Text.Length > 0))
        {
            _chat.AddNotice(Strings.Format(Strings.NoticeInstructionsData, Names(instructions.Data.Where(file => file.Text.Length > 0))));
        }

        if (all.Any(file => file.IsCut))
        {
            _chat.AddNotice(Strings.Format(Strings.NoticeInstructionsCut, Names(all.Where(file => file.IsCut))));
        }

        foreach (var file in all.Where(file => file.Problem is not null))
        {
            _chat.AddNotice(Strings.Format(Strings.NoticeInstructionsUnreadable, WorkspaceFolders.Display(file.Path), file.Problem));
        }
    }

    private void ReportChanges(IReadOnlyList<InstructionChange> changes)
    {
        foreach (var change in changes)
        {
            var shown = WorkspaceFolders.Display(change.Path);
            if (change.IsOnlySecrets)
            {
                _chat.AddNotice(Strings.Format(Strings.NoticeInstructionsSecretChanged, shown));
                continue;
            }

            _chat.AddNotice(Strings.Format(Strings.NoticeInstructionsChanged, shown), Strings.ShowChanges, () => _ = ChoiceWindow.AskAsync(
                this,
                Strings.Format(Strings.InstructionChangesTitle, shown),
                Strings.InstructionChangesMessage,
                LineDiff.Describe(change.Before, change.After),
                hasDefault: true,
                Strings.Close));
        }
    }
}
