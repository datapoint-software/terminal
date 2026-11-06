using System.Globalization;
using System.Text;
using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Agent.Tools;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Agent;

public sealed partial class TerminalAgent
{
    private static readonly TimeSpan s_quietPeriod = TimeSpan.FromMilliseconds(1500);

    private long? _runningCommandLine;

    private List<AITool> CreateTools() =>
    [
        new ToolFunction(
            "run_command",
            "Types a shell command into the user's terminal, presses Enter, and returns its output and exit code once it finishes. "
            + "The user sees it run and may have to approve it first. One command per call, on a single line.",
            """
            {"type":"object","properties":{
              "command":{"type":"string","description":"The command line to type, without a trailing newline."},
              "purpose":{"type":"string","description":"Why you want to run it, in a short sentence the user reads when approving it."}},
             "required":["command","purpose"]}
            """,
            RunCommandAsync),
        new ToolFunction(
            "wait_for_command",
            "Waits for the command you started last to finish, e.g. one that was still running when run_command returned, or one "
            + "waiting for the user to type a password. Returns its output and exit code.",
            """
            {"type":"object","properties":{
              "expects_user_input":{"type":"boolean","description":"True when the command waits for the user to type into the terminal (e.g. a password); their typing then doesn't interrupt you."}},
             "required":[]}
            """,
            WaitForCommandAsync),
        new ToolFunction(
            "read_screen",
            "Returns the text currently visible in the terminal, and whether it's at a prompt.",
            """{"type":"object","properties":{},"required":[]}""",
            ReadScreenAsync),
        new ToolFunction(
            "ask_user",
            "Asks the user a question in the chat and waits for the answer. Give options when there are a few clear choices; the "
            + "user can still answer in their own words.",
            """
            {"type":"object","properties":{
              "question":{"type":"string"},
              "options":{"type":"array","items":{"type":"string"},"description":"Up to 6 short choices; omit for an open question."}},
             "required":["question"]}
            """,
            AskUserAsync),
    ];

    private async Task<string> RunCommandAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var proposed = ToolArguments.GetString(arguments, "command")?.Trim();
        var purpose = ToolArguments.GetString(arguments, "purpose") ?? string.Empty;
        var problem = CommandCheck.Problem(proposed) ?? TerminalProblem();
        if (problem is not null)
        {
            return "Not run: " + problem;
        }

        var approval = await ApproveAsync(proposed!, purpose, cancellationToken).ConfigureAwait(false);
        if (!approval.Approved)
        {
            return "Not run: the user declined." + (string.IsNullOrWhiteSpace(approval.Feedback) ? string.Empty : " They said: " + approval.Feedback);
        }

        // Check again: the user may have edited the command, or typed into the terminal while deciding.
        var command = approval.Command.Trim();
        problem = CommandCheck.Problem(command) ?? TerminalProblem();
        if (problem is not null)
        {
            return "Not run: " + problem;
        }

        var edited = string.Equals(command, proposed, StringComparison.Ordinal) ? string.Empty : $"The user edited the command to: {command}\n";
        return edited + await TypeAndWaitAsync(command, purpose, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> TypeAndWaitAsync(string command, string purpose, CancellationToken cancellationToken)
    {
        var start = _terminal.GetState().NextOutputLine;
        _runningCommandLine = start;
        Raise(new AgentEventArgs(AgentEventKind.CommandStarted, purpose) { Command = command });
        SetStatus(AgentStatus.RunningCommand);
        using var signal = new OutputSignal(_terminal);
        _terminal.TypeCommand(command);
        return await WaitForResultAsync(start, signal, command, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> WaitForCommandAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        if (_runningCommandLine is not { } start)
        {
            return "There is no command of yours to wait for.";
        }

        var expectsInput = ToolArguments.GetString(arguments, "expects_user_input") is { } value && bool.TryParse(value, out var flag) && flag;
        _isAwaitingTerminalInput = expectsInput;
        SetStatus(expectsInput ? AgentStatus.WaitingForUser : AgentStatus.RunningCommand);
        try
        {
            using var signal = new OutputSignal(_terminal);
            return await WaitForResultAsync(start, signal, command: null, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _isAwaitingTerminalInput = false;
        }
    }

    private Task<string> ReadScreenAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var state = _terminal.GetState();
        var text = new StringBuilder();
        text.Append(state.HasShellIntegration ? (state.IsAtPrompt ? "At a prompt" : "A command is running") : "The shell doesn't report prompts")
            .Append(state.IsAlternateScreen ? "; a full-screen program is open" : string.Empty)
            .Append(state.WorkingDirectory is null ? string.Empty : "; working folder: " + state.WorkingDirectory)
            .Append(".\n");
        text.Append(Untrusted(_terminal.GetVisibleText()));
        return Task.FromResult(text.ToString());
    }

    private async Task<string> AskUserAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var question = ToolArguments.GetString(arguments, "question");
        if (string.IsNullOrWhiteSpace(question))
        {
            return "Not asked: the question is empty.";
        }

        var options = ToolArguments.GetStrings(arguments, "options").Where(option => !string.IsNullOrWhiteSpace(option)).Take(6).ToList();
        SetStatus(AgentStatus.WaitingForUser);
        var answer = await _user.AskAsync(new AgentQuestion(question, options), cancellationToken).ConfigureAwait(false);
        SetStatus(AgentStatus.Thinking);
        return answer is null ? "The user didn't answer." : "The user answered: " + answer;
    }

    private async Task<CommandApproval> ApproveAsync(string command, string purpose, CancellationToken cancellationToken)
    {
        var risk = CommandClassifier.Classify(command);
        var needsApproval = Mode switch
        {
            ApprovalMode.Auto => risk == CommandRisk.AlwaysAsk,
            ApprovalMode.AskForRisky => risk != CommandRisk.ReadOnly,
            _ => true,
        };
        if (!needsApproval)
        {
            return CommandApproval.Approve(command);
        }

        SetStatus(AgentStatus.WaitingForApproval);
        return await _user.RequestApprovalAsync(new CommandProposal(command, purpose, risk), cancellationToken).ConfigureAwait(false);
    }

    private string? TerminalProblem()
    {
        var state = _terminal.GetState();
        if (state.IsAlternateScreen)
        {
            return "a full-screen program is open in the terminal. Ask the user to close it, or use read_screen.";
        }

        if (state.HasShellIntegration && !state.IsAtPrompt)
        {
            return "a command is still running in the terminal. Use wait_for_command or read_screen.";
        }

        return state.PendingInput.Length > 0
            ? "the user has started typing at the prompt. Ask them what they want to do."
            : null;
    }

    private async Task<string> WaitForResultAsync(long start, OutputSignal signal, string? command, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + _options.CommandTimeout;
        while (true)
        {
            var hasIntegration = _terminal.GetState().HasShellIntegration;
            var outcome = TryFinish(start, signal, command, hasIntegration, deadline);
            if (outcome is not null)
            {
                return outcome;
            }

            await signal.WaitAsync(hasIntegration ? deadline - DateTimeOffset.UtcNow : s_quietPeriod, cancellationToken).ConfigureAwait(false);
        }
    }

    // Without shell integration, a command counts as finished once its output has been quiet for a moment, which is only a guess.
    private string? TryFinish(long start, OutputSignal signal, string? command, bool hasIntegration, DateTimeOffset deadline)
    {
        var result = _terminal.GetCommand(start);
        if (result is { IsFinished: true })
        {
            return Finish(command, result.Output, result.ExitCode, isFinished: true);
        }

        var isQuiet = !hasIntegration && DateTimeOffset.UtcNow - signal.LastOutputAt >= s_quietPeriod;
        var isOver = isQuiet || signal.HasEnded;
        return isOver || DateTimeOffset.UtcNow >= deadline
            ? Finish(command, result?.Output ?? _terminal.GetVisibleText(), exitCode: null, isOver)
            : null;
    }

    private string Finish(string? command, string output, int? exitCode, bool isFinished)
    {
        if (isFinished)
        {
            _runningCommandLine = null;
        }

        Raise(new AgentEventArgs(AgentEventKind.CommandFinished, isFinished ? "Finished" : "Still running") { Command = command, ExitCode = exitCode });
        SetStatus(AgentStatus.Thinking);
        var status = (isFinished, exitCode) switch
        {
            (true, { } code) => "Exit code: " + code.ToString(CultureInfo.InvariantCulture),
            (true, null) => "The output has stopped; the shell doesn't report exit codes, so check the output.",
            _ => $"Still running after {_options.CommandTimeout.TotalSeconds:0} seconds. Use wait_for_command to keep waiting, or ask the user.",
        };
        return status + "\n" + Untrusted(output);
    }

    // Terminal text is marked as data so the model doesn't take it for instructions.
    private string Untrusted(string text)
    {
        var redacted = _redactor.Redact(text);
        var limit = _options.MaxOutputCharacters;
        if (redacted.Length > limit)
        {
            var half = limit / 2;
            redacted = redacted[..half] + $"\n[... {redacted.Length - limit} characters left out ...]\n" + redacted[^half..];
        }

        return "<terminal_output>\n" + redacted.Replace("</terminal_output>", "</terminal_output_>", StringComparison.OrdinalIgnoreCase) + "\n</terminal_output>";
    }
}
