using System.Text;
using Avalonia.Threading;
using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Agent.Memory;
using Datapoint.Terminal.Agent.Projects;
using Datapoint.Terminal.Agent.Projects.Ignore;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Shell;

public sealed partial class MainWindow
{
    // Read on the agent's thread when a conversation starts.
    private volatile ProjectTrustRecord? _trust;

    private IReadOnlyList<string> ProjectInstructionFiles(string? globalFile) =>
        InstructionFiles.FindForProject(Project.Folder, ProjectFolders.Home, globalFile);

    // Asks when the project has something to trust and no decision covers it yet; from the menu, it always asks.
    private async Task ReviewTrustAsync(bool always)
    {
        var files = ProjectInstructionFiles(InstructionFiles.FindGlobal(ProjectFolders.Global));
        var record = _settings.TrustFor(Project.Folder);
        var state = ProjectTrust.Evaluate(Project.IsDefault, Project.Settings.Connections.Count > 0, files, record);
        if (state == TrustState.Trusted && record is null && !Project.IsDefault)
        {
            record = new ProjectTrustRecord { Folder = Project.Folder, IsTrusted = true, InstructionFiles = files };
            _settings.SetTrust(record);
        }

        if (state == TrustState.Ask || (always && !Project.IsDefault))
        {
            var choice = await ChoiceWindow.AskAsync(
                this,
                Strings.Format(Strings.TrustTitle, Project.Name),
                Strings.TrustMessage,
                TrustDetails(files, record),
                hasDefault: false,
                Strings.TrustDont,
                Strings.TrustTrust).ConfigureAwait(true);

            // Closing the question decides nothing: the project stays as it was, and is asked about again next time.
            if (choice is { } answer)
            {
                record = new ProjectTrustRecord { Folder = Project.Folder, IsTrusted = answer == 1, InstructionFiles = files };
                _settings.SetTrust(record);
            }
        }

        _trust = record;
    }

    private string TrustDetails(IReadOnlyList<string> files, ProjectTrustRecord? record)
    {
        var connections = Project.Settings.Connections;
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

    private static string DescribeFile(string file, ProjectTrustRecord? record)
    {
        var shown = ProjectFolders.Display(file);
        return record is not null && !record.Covers(file) ? Strings.Format(Strings.TrustNew, shown) : shown;
    }

    private static string DescribeConnection(ProjectConnection connection)
    {
        var user = connection.User is null ? string.Empty : connection.User + "@";
        var port = connection.Port is { } number ? $":{number}" : string.Empty;
        var target = connection.SshHost ?? user + connection.Host + port;
        var environment = connection.Environment is null ? string.Empty : $" ({connection.Environment})";
        return $"{connection.Name} → {target}{environment}";
    }

    // The global file is the user's own; the project's files are instructions only when the user trusted them as they were shown.
    // Files listed in .terminalignore aren't read at all, and those that look like they hold a secret wait for the user's choice.
    private async Task<AgentInstructions> LoadInstructionsAsync(CancellationToken cancellationToken)
    {
        var global = InstructionFiles.FindGlobal(ProjectFolders.Global);
        List<string> paths = [.. global is null ? [] : new[] { global }, .. ProjectInstructionFiles(global)];
        var ignored = paths.Where(path => TerminalIgnore.IsIgnored(path, Project.Folder, ProjectFolders.Home)).ToList();
        var files = InstructionFiles.Read([.. paths.Except(ignored, ProjectFolders.Comparer)]);
        var changes = _windows.InstructionSnapshots.Update(files, _redactor);
        var (reviewed, skipped) = await ReviewSecretsAsync(files, cancellationToken).ConfigureAwait(false);
        var (memory, ignoredMemory, skippedMemory) = await LoadMemoryAsync(cancellationToken).ConfigureAwait(false);
        ignored.AddRange(ignoredMemory);
        skipped.AddRange(skippedMemory);
        var trust = _trust;
        var isTrusted = (InstructionFile file) => Project.IsDefault
            || ProjectFolders.AreSame(file.Path, global ?? string.Empty)
            || ProjectFolders.AreSame(file.Path, GlobalMemoryPath)
            || (trust is { IsTrusted: true } && trust.Covers(file.Path));
        var instructions = new AgentInstructions(
            [.. reviewed.Where(isTrusted)],
            [.. memory.Where(file => !isTrusted(file)), .. reviewed.Where(file => !isTrusted(file))])
        {
            Ignored = TerminalIgnore.Describe(Project.Folder, ProjectFolders.Home),
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

    private static string GlobalMemoryPath => Path.Combine(ProjectFolders.Global, MemoryFile.FileName);

    private string? ProjectMemoryPath => Project.IsDefault ? null : Path.Combine(Project.PersonalPath, MemoryFile.FileName);

    // Memory goes through the same checks as instruction files, but not their change notices: the agent changes it, in view.
    private async Task<(List<InstructionFile> Memory, List<string> Ignored, List<string> Skipped)> LoadMemoryAsync(CancellationToken cancellationToken)
    {
        var paths = new[] { GlobalMemoryPath, ProjectMemoryPath }.OfType<string>().Where(File.Exists).ToList();
        var ignored = paths.Where(path => TerminalIgnore.IsIgnored(path, Project.Folder, ProjectFolders.Home)).ToList();
        var files = InstructionFiles.Read([.. paths.Except(ignored, ProjectFolders.Comparer)], 2 * MemoryFile.MaxBytes);
        var (reviewed, skipped) = await ReviewSecretsAsync(files, cancellationToken).ConfigureAwait(false);
        return (reviewed, ignored, skipped);
    }

    private void ReportNotSent(string format, List<string> paths)
    {
        if (paths.Count == 0)
        {
            return;
        }

        _chat.AddNotice(Strings.Format(format, string.Join(", ", paths.Select(ProjectFolders.Display))));
    }

    private void ReportInstructions(AgentInstructions instructions)
    {
        static string Names(IEnumerable<InstructionFile> files) => string.Join(", ", files.Select(file => ProjectFolders.Display(file.Path)));
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
            _chat.AddNotice(Strings.Format(Strings.NoticeInstructionsUnreadable, ProjectFolders.Display(file.Path), file.Problem));
        }
    }

    private void ReportChanges(IReadOnlyList<InstructionChange> changes)
    {
        foreach (var change in changes)
        {
            var shown = ProjectFolders.Display(change.Path);
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
