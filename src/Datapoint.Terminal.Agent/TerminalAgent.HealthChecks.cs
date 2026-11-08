using Datapoint.Terminal.Agent.Skills;

namespace Datapoint.Terminal.Agent;

// A rollout's health check, run by the application in each host's terminal, where the user sees it, until it has kept passing
// for the skill's healthy-for or the timeout runs out. A host whose terminal went away, e.g. for a reboot, is reconnected, and
// its timeout starts again once it's back, so a slow reboot doesn't count against it.
public sealed partial class TerminalAgent
{
    private const int MaxCheckOutput = 400;

    private async Task CheckBatchAsync(SkillRun run, CancellationToken cancellationToken)
    {
        var checking = run.Current.Where(host => host.State == RolloutHostState.Checking).Select(host => host.Name).ToList();
        if (checking.Count == 0)
        {
            return;
        }

        SetStatus(AgentStatus.RunningCommand);
        await Task.WhenAll(checking.Select(host => CheckHealthAsync(run, host, cancellationToken))).ConfigureAwait(false);
    }

    private async Task CheckHealthAsync(SkillRun run, string host, CancellationToken cancellationToken)
    {
        var check = new HealthCheck(run, host, DateTimeOffset.UtcNow + run.Skill.Rollout.Timeout);
        run.Set(host, RolloutHostState.Checking);
        while (!await CheckOnceAsync(check, cancellationToken).ConfigureAwait(false))
        {
            await Task.Delay(_options.HealthCheckInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    // True once the host's outcome is known.
    private async Task<bool> CheckOnceAsync(HealthCheck check, CancellationToken cancellationToken)
    {
        var rollout = check.Run.Skill.Rollout;
        if (TerminalFor(check.Host) is not { } terminal)
        {
            return await ReconnectAsync(check, cancellationToken).ConfigureAwait(false);
        }

        if (check.GoneSince is { } gone)
        {
            Raise(new AgentEventArgs(AgentEventKind.Notice, Say("NoticeReconnected", check.Host, SkillDocument.FormatDuration(DateTimeOffset.UtcNow - gone))) { Terminal = terminal.Name });
            check.GoneSince = null;
            check.Deadline = DateTimeOffset.UtcNow + rollout.Timeout;
        }

        if (TerminalProblem(terminal) is null && await RunCheckAsync(terminal, rollout.HealthCheck!, cancellationToken).ConfigureAwait(false) is var result)
        {
            if (Judge(check, terminal, result))
            {
                return true;
            }
        }

        return TimedOut(check);
    }

    // True once the outcome is known: healthy for long enough, or a shell that can't tell.
    private bool Judge(HealthCheck check, AgentTerminal terminal, CommandResult? result)
    {
        var rollout = check.Run.Skill.Rollout;
        if (result?.ExitCode is not { } exitCode)
        {
            if (terminal.Terminal.GetState().HasShellIntegration || TerminalFor(check.Host) is null)
            {
                check.PassingSince = null;
                return false;
            }

            check.Run.Set(check.Host, RolloutHostState.Failed, "the shell doesn't report exit codes, so the health check can't tell", failedCheck: true);
            return true;
        }

        if (exitCode != 0)
        {
            check.PassingSince = null;
            check.LastFailure = $"exit code {exitCode}" + LastLines(result.Output);
            check.Run.Set(check.Host, RolloutHostState.Checking, "Not healthy yet");
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        check.PassingSince ??= now;
        var healthy = now - check.PassingSince.Value;
        if (healthy >= rollout.HealthyFor)
        {
            check.Run.Set(check.Host, RolloutHostState.Done, "healthy for " + SkillDocument.FormatDuration(rollout.HealthyFor));
            return true;
        }

        check.Run.Set(check.Host, RolloutHostState.Checking, null, healthy);
        return false;
    }

    private static bool TimedOut(HealthCheck check)
    {
        if (DateTimeOffset.UtcNow < check.Deadline)
        {
            return false;
        }

        var timeout = SkillDocument.FormatDuration(check.Run.Skill.Rollout.Timeout);
        check.Run.Set(check.Host, RolloutHostState.Failed, $"the health check kept failing for {timeout}" + (check.LastFailure is { } last ? ": " + last : string.Empty), failedCheck: true);
        return true;
    }

    private async Task<bool> ReconnectAsync(HealthCheck check, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        check.GoneSince ??= now;
        check.PassingSince = null;
        var timeout = check.Run.Skill.Rollout.Timeout;
        if (now - check.GoneSince.Value >= timeout)
        {
            check.Run.Set(check.Host, RolloutHostState.Failed, $"it didn't come back within {SkillDocument.FormatDuration(timeout)}", failedCheck: true);
            return true;
        }

        check.Run.Set(check.Host, RolloutHostState.Reconnecting, "Waiting for it to come back");
        await (TerminalOpener?.ReconnectAsync(check.Host, cancellationToken) ?? Task.FromResult<string?>(null)).ConfigureAwait(false);
        return false;
    }

    // Typed like the agent's commands, so the user sees it, but not added to the chat each time it's run.
    private async Task<CommandResult?> RunCheckAsync(AgentTerminal terminal, string command, CancellationToken cancellationToken)
    {
        var start = terminal.Terminal.GetState().NextOutputLine;
        var deadline = DateTimeOffset.UtcNow + _options.CommandTimeout;
        using var signal = new OutputSignal(terminal.Terminal);
        terminal.Terminal.TypeCommand(command);
        while (true)
        {
            var result = terminal.Terminal.GetCommand(start);
            var isQuiet = !terminal.Terminal.GetState().HasShellIntegration && DateTimeOffset.UtcNow - signal.LastOutputAt >= s_quietPeriod;
            if (result is { IsFinished: true } || signal.HasEnded || isQuiet || DateTimeOffset.UtcNow >= deadline)
            {
                return result;
            }

            await signal.WaitAsync(s_quietPeriod, cancellationToken).ConfigureAwait(false);
        }
    }

    // The end of a failing check's output, for the model and the user to see why; it's output from the host, so it's redacted.
    private string LastLines(string output)
    {
        var lines = _redactor.Redact(output).TrimEnd().Split('\n').TakeLast(3);
        var text = string.Join(" / ", lines.Select(line => line.Trim()).Where(line => line.Length > 0));
        return text.Length == 0 ? string.Empty : ", output \"" + (text.Length > MaxCheckOutput ? text[..MaxCheckOutput] + "…" : text) + "\"";
    }

    private sealed class HealthCheck(SkillRun run, string host, DateTimeOffset deadline)
    {
        public SkillRun Run { get; } = run;

        public string Host { get; } = host;

        public DateTimeOffset Deadline { get; set; } = deadline;

        public DateTimeOffset? PassingSince { get; set; }

        public DateTimeOffset? GoneSince { get; set; }

        public string? LastFailure { get; set; }
    }
}
