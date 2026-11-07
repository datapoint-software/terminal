using System.Text;
using Datapoint.Terminal.Agent.Files;
using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Agent.Tools;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Agent;

public sealed partial class TerminalAgent
{
    private sealed record PreparedEdit(AgentTerminal Terminal, string Path, HostFile File, string Before, string After);

    private async Task<string> EditFileAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var (edit, problem) = await PrepareEditAsync(arguments, cancellationToken).ConfigureAwait(false);
        if (edit is null)
        {
            return problem!;
        }

        var purpose = ToolArguments.GetString(arguments, "purpose") ?? string.Empty;
        var diff = TextDiff.Unified(edit.Path, edit.Before, edit.After);
        var decision = await ApproveEditAsync(edit, diff, purpose, PlanStep(arguments), cancellationToken).ConfigureAwait(false);
        if (decision is not null)
        {
            return decision;
        }

        Raise(new AgentEventArgs(AgentEventKind.Notice, $"Changing {edit.Path} on {edit.Terminal.Name}:\n{diff}") { Terminal = edit.Terminal.Name });
        var step = CheckPlan(edit.Terminal, CommandRisk.Changes, PlanStep(arguments)).Step;
        return await ApplyEditAsync(edit, purpose, step, cancellationToken).ConfigureAwait(false);
    }

    // Reads the file and works out the new text, or says why not, before anything is asked.
    private async Task<(PreparedEdit? Edit, string? Problem)> PrepareEditAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        if (FileTarget(arguments, out var terminal, out var path) is { } refusal || (refusal = TerminalProblem(terminal!)) is not null)
        {
            return (null, "Not changed: " + refusal);
        }

        var edits = ReadEdits(arguments);
        var content = ToolArguments.GetString(arguments, "content");
        var written = string.Concat(edits.Select(item => item.NewText)) + content;
        if (_redactor.Find(written).Count > 0)
        {
            return (null, "Not changed: the new text looks like it holds a secret. Leave a placeholder and ask the user to fill it in.");
        }

        var (file, problem) = await TryReadFileAsync(terminal!, path!, cancellationToken).ConfigureAwait(false);
        if (file is null)
        {
            return (null, "Not changed: " + problem);
        }

        var (before, after, textProblem) = NewText(file, edits, content);
        return textProblem is not null ? (null, "Not changed: " + textProblem)
            : string.Equals(before, after, StringComparison.Ordinal) ? (null, "Already up to date: the edits change nothing.")
            : (new PreparedEdit(terminal!, path!, file, before!, after!), null);
    }

    private static (string? Before, string? After, string? Problem) NewText(HostFile file, List<FileEdit> edits, string? content)
    {
        if (!file.Exists)
        {
            return content is null ? (null, null, "the file doesn't exist. Give content to create it.") : (string.Empty, content, null);
        }

        if (file.IsTruncated)
        {
            return (null, null, $"the file is larger than {MaxFileBytes / 1024} KB. Edit it with the user in an editor.");
        }

        if (Decode(file.Content) is not { } before)
        {
            return (null, null, "the file isn't text (it isn't UTF-8, or it has NUL bytes).");
        }

        if (edits.Count == 0)
        {
            return (null, null, "the file exists; give edits rather than content.");
        }

        var after = FileEdits.Apply(before, edits, out var problem);
        return (before, after, problem);
    }

    // Null to go ahead; otherwise what to tell the model. Edits ask as changes do, and in Auto follow the plan.
    private async Task<string?> ApproveEditAsync(PreparedEdit edit, string diff, string purpose, int? step, CancellationToken cancellationToken)
    {
        var plan = CheckPlan(edit.Terminal, CommandRisk.Changes, step);
        if (plan.Problem is not null)
        {
            return "Not changed: " + plan.Problem;
        }

        if (edit.Terminal.Mode == ApprovalMode.Auto && !plan.IsOutside)
        {
            return null;
        }

        SetStatus(AgentStatus.WaitingForApproval, edit.Terminal);
        var proposal = new CommandProposal(diff, purpose, CommandRisk.Changes, edit.Terminal.Name, edit.Terminal.Environment)
        {
            Kind = ProposalKind.EditFile,
            PlanStep = plan.Step,
            IsOutsidePlan = plan.IsOutside,
        };
        var approval = await _user.RequestApprovalAsync(proposal, cancellationToken).ConfigureAwait(false);
        SetStatus(AgentStatus.Thinking);
        return approval.Approved
            ? null
            : "Not changed: the user declined." + (string.IsNullOrWhiteSpace(approval.Feedback) ? string.Empty : " They said: " + approval.Feedback);
    }

    // The file is read again first, so a change made while the user was deciding isn't overwritten.
    private async Task<string> ApplyEditAsync(PreparedEdit edit, string purpose, int? planStep, CancellationToken cancellationToken)
    {
        var (now, problem) = await TryReadFileAsync(edit.Terminal, edit.Path, cancellationToken).ConfigureAwait(false);
        if (now is null || now.Exists != edit.File.Exists || !now.Content.AsSpan().SequenceEqual(edit.File.Content))
        {
            return "Not changed: " + (problem ?? "the file changed while the edit waited. Read it again.");
        }

        string staged;
        try
        {
            Raise(new AgentEventArgs(AgentEventKind.Notice, $"Saved the new {edit.Path} to {HostFolders.Staging} on {edit.Terminal.Name}'s machine, beside the terminal.") { Terminal = edit.Terminal.Name });
            staged = await edit.Terminal.Files!.StageAsync(Encoding.UTF8.GetBytes(edit.After), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException
            or Sessions.Ssh.Channels.SshChannelException or Sessions.Ssh.SshDisconnectedException)
        {
            return "Not changed: " + exception.Message;
        }

        var backup = edit.File.Exists ? FileEditCommand.BackupPath(edit.Path, Clock.GetLocalNow()) : null;
        var useSudo = !edit.File.IsWritable;
        var command = FileEditCommand.Build(edit.Path, staged, backup, useSudo);
        if (Check(edit.Terminal, command) is { } busy)
        {
            return "Not changed: " + busy;
        }

        var undo = FileEditCommand.Undo(edit.Path, backup, useSudo);
        var saved = backup is null ? $"Creates {edit.Path}." : $"Backup: {backup}.";
        return $"{saved} To undo: {undo}\n" + await TypeAndWaitAsync(edit.Terminal, command, purpose, planStep, cancellationToken).ConfigureAwait(false);
    }
}
