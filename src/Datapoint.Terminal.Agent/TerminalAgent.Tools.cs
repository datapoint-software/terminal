using System.Globalization;
using System.Text;
using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Agent.Tools;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Agent;

public sealed partial class TerminalAgent
{
    private static readonly TimeSpan s_quietPeriod = TimeSpan.FromMilliseconds(1500);

    private List<AITool> CreateTools() =>
    [
        new ToolFunction(
            "run_command",
            "Types a shell command into one of the user's terminals, presses Enter, and returns its output and exit code once it "
            + "finishes. The user sees it run and may have to approve it first. One command per call, on a single line.",
            """
            {"type":"object","properties":{
              "terminal":{"type":"string","description":"The name of the terminal, as listed in the user's message. May be left out when only one is open."},
              "command":{"type":"string","description":"The command line to type, without a trailing newline."},
              "purpose":{"type":"string","description":"Why you want to run it, in a short sentence the user reads when approving it."},
              "plan_step":{"type":"integer","description":"The number of the approved plan's step this command carries out. Leave it out when the command isn't in the plan."}},
             "required":["command","purpose"]}
            """,
            RunCommandAsync),
        new ToolFunction(
            "wait_for_command",
            "Waits for the command you started last in a terminal to finish, e.g. one that was still running when run_command returned, or one "
            + "waiting for the user to type a password. Returns its output and exit code.",
            """
            {"type":"object","properties":{
              "terminal":{"type":"string","description":"The name of the terminal, as listed in the user's message. May be left out when only one is open."},
              "expects_user_input":{"type":"boolean","description":"True when the command waits for the user to type into the terminal (e.g. a password); their typing then doesn't interrupt you."}},
             "required":[]}
            """,
            WaitForCommandAsync),
        new ToolFunction(
            "read_screen",
            "Returns the text currently visible in a terminal, and whether it's at a prompt.",
            """
            {"type":"object","properties":{
              "terminal":{"type":"string","description":"The name of the terminal, as listed in the user's message. May be left out when only one is open."}},
             "required":[]}
            """,
            ReadScreenAsync),
        new ToolFunction(
            "ask_user",
            "Asks the user a question in the chat and waits for the answer. Give options when there are a few clear choices; the "
            + "user can still answer in their own words.",
            """
            {"type":"object","properties":{
              "question":{"type":"string","description":"The question alone, in one short sentence. It's shown in bold."},
              "details":{"type":"string","description":"What the user needs to know to decide, in short Markdown, shown above the question. Omit when the question says it all."},
              "terminal":{"type":"string","description":"The terminal the question is about, if any; it's named above the question."},
              "options":{"type":"array","maxItems":6,"description":"Up to 6 choices; omit for an open question.","items":{"type":"object","properties":{
                "label":{"type":"string","description":"A few words, shown on the button."},
                "description":{"type":"string","description":"One short sentence on what the choice does or costs."},
                "recommended":{"type":"boolean","description":"True on the one option you recommend, if any."}},
               "required":["label"]}}},"required":["question"]}
            """,
            AskUserAsync),
        .. FeatureTools(),
    ];

    private List<AITool> FeatureTools() =>
    [
        .. OpenTools(),
        .. PlanModeTools(),
        .. LookTools(),
        .. PlanTools(),
        .. FileTools(),
        .. WatchTools(),
        .. KeysTools(),
        .. MemoryTools(),
        .. HostDocsTools(),
        .. SkillTools(),
        .. HistoryTools(),
        .. WebTools(),
    ];

    private async Task<string> RunCommandAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var proposed = ToolArguments.GetString(arguments, "command")?.Trim();
        var purpose = ToolArguments.GetString(arguments, "purpose") ?? string.Empty;
        if (FindTerminal(arguments, out var problem) is not { } terminal)
        {
            return "Not run: " + problem;
        }

        problem = Check(terminal, proposed);
        if (problem is not null)
        {
            return "Not run: " + problem;
        }

        var plan = CheckPlan(terminal, CommandClassifier.Classify(proposed!, LearnedCommands), PlanStep(arguments));
        if (plan.Problem is not null)
        {
            return "Not run: " + plan.Problem;
        }

        var approval = await ApproveAsync(terminal, proposed!, purpose, plan, cancellationToken).ConfigureAwait(false);
        if (!approval.Approved)
        {
            return Declined(approval);
        }

        // Check again: the user may have edited the command, or typed into the terminal while deciding.
        var command = approval.Command.Trim();
        problem = Check(terminal, command);
        if (problem is not null)
        {
            return "Not run: " + problem;
        }

        var edited = string.Equals(command, proposed, StringComparison.Ordinal) ? string.Empty : $"The user edited the command to: {command}\n";
        return edited + await TypeAndWaitAsync(terminal, command, purpose, plan.Step, cancellationToken).ConfigureAwait(false);
    }

    private static PlanStepState StepState(bool isFinished, int? exitCode) => (isFinished, exitCode) switch
    {
        (false, _) => PlanStepState.Running,
        (true, 0) => PlanStepState.Succeeded,
        (true, null) => PlanStepState.Finished,
        _ => PlanStepState.Failed,
    };

    private static string? Check(AgentTerminal terminal, string? command) => CommandCheck.Problem(command) ?? TerminalProblem(terminal);

    private static string Declined(CommandApproval approval) =>
        "Not run: the user declined." + (string.IsNullOrWhiteSpace(approval.Feedback) ? string.Empty : " They said: " + approval.Feedback);

    private async Task<string> TypeAndWaitAsync(AgentTerminal terminal, string command, string purpose, int? planStep, CancellationToken cancellationToken)
    {
        var start = terminal.Terminal.GetState().NextOutputLine;
        terminal.RunningCommandLine = start;
        terminal.RunningPlanStep = planStep;
        Raise(new AgentEventArgs(AgentEventKind.CommandStarted, purpose) { Command = command, Terminal = terminal.Name, PlanStep = planStep, StepState = PlanStepState.Running });
        SetStatus(AgentStatus.RunningCommand, terminal);
        using var signal = new OutputSignal(terminal.Terminal);
        terminal.Terminal.TypeCommand(command);
        return await WaitForResultAsync(terminal, start, signal, command, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> WaitForCommandAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        if (FindTerminal(arguments, out var problem) is not { } terminal)
        {
            return problem!;
        }

        if (terminal.RunningCommandLine is not { } start)
        {
            return $"There is no command of yours to wait for in {terminal.Name}.";
        }

        var expectsInput = ToolArguments.GetString(arguments, "expects_user_input") is { } value && bool.TryParse(value, out var flag) && flag;
        terminal.IsAwaitingInput = expectsInput;
        SetStatus(expectsInput ? AgentStatus.WaitingForUser : AgentStatus.RunningCommand, terminal);
        try
        {
            using var signal = new OutputSignal(terminal.Terminal);
            return await WaitForResultAsync(terminal, start, signal, command: null, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            terminal.IsAwaitingInput = false;
        }
    }

    private Task<string> ReadScreenAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        if (FindTerminal(arguments, out var problem) is not { } terminal)
        {
            return Task.FromResult(problem!);
        }

        var state = terminal.Terminal.GetState();
        var text = new StringBuilder();
        text.Append(state.Session is { } session
                ? $"In a session started in this terminal ({session}): commands go to it, and its prompts and exit codes aren't reported"
                : state.HasShellIntegration ? (state.IsAtPrompt ? "At a prompt" : "A command is running") : "The shell doesn't report prompts")
            .Append(state.IsAlternateScreen ? "; a full-screen program is open" : string.Empty)
            .Append(state.IsInputHidden ? "; a program is waiting for hidden input, such as a password" : string.Empty)
            .Append(state.WorkingDirectory is null ? string.Empty : "; working folder: " + state.WorkingDirectory)
            .Append(".\n");
        text.Append(Untrusted(terminal.Terminal.GetVisibleText()));
        return Task.FromResult(text.ToString());
    }

    private async Task<string> AskUserAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var question = ToolArguments.GetString(arguments, "question");
        if (string.IsNullOrWhiteSpace(question))
        {
            return "Not asked: the question is empty.";
        }

        var details = ToolArguments.GetString(arguments, "details");
        var terminal = FindTerminal(arguments, out _);
        var asked = new AgentQuestion(question.Trim(), QuestionOptions.Parse(arguments))
        {
            Details = string.IsNullOrWhiteSpace(details) ? null : details.Trim(),
            Terminal = terminal?.Name,
            Session = terminal?.Terminal.GetState().Session,
        };
        SetStatus(AgentStatus.WaitingForUser);
        var answer = await AskAsync(asked, cancellationToken).ConfigureAwait(false);
        SetStatus(AgentStatus.Thinking);
        return answer is null ? "The user didn't answer." : "The user answered: " + answer;
    }

    private async Task<CommandApproval> ApproveAsync(AgentTerminal terminal, string command, string purpose, PlanCheck plan, CancellationToken cancellationToken)
    {
        var risk = CommandClassifier.Classify(command, LearnedCommands);
        var ignored = IgnoredFileCheck?.Invoke(terminal, command);
        var outgoing = OutgoingCheck(command);
        if (!NeedsAsking(terminal, risk, ignored, plan, outgoing))
        {
            return CommandApproval.Approve(command);
        }

        var canCarry = CanCarry(ignored, risk, plan, outgoing);
        if (canCarry && CarriedApproval(terminal, command) is { } carried)
        {
            return carried;
        }

        SetStatus(AgentStatus.WaitingForApproval, terminal);
        var proposal = new CommandProposal(command, purpose, risk, terminal.Name, terminal.Environment)
        {
            IgnoredFile = ignored,
            Session = terminal.Terminal.GetState().Session,
            PlanStep = plan.Step,
            IsOutsidePlan = plan.IsOutside,
            AlsoApproves = canCarry ? SameEnvironment(terminal) : [],
            IsAfterWeb = outgoing.IsAfterWeb,
            TerminalText = outgoing.TerminalText,
        };
        var approval = await RequestApprovalAsync(proposal, cancellationToken).ConfigureAwait(false);
        RememberApproval(terminal, approval, canCarry);
        return approval;
    }

    private bool NeedsAsking(AgentTerminal terminal, CommandRisk risk, string? ignored, PlanCheck plan, OutgoingRisk outgoing) =>
        ignored is not null || (Mode != ApprovalMode.IgnorePermissions && (plan.IsOutside || outgoing.Asks || (NeedsApproval(Mode, risk) && !MayLook(terminal, risk))));

    // A command that can reach another machine asks first once web text is in the conversation, or when it carries text the
    // terminals showed, whatever the approval mode: either may be a prompt injection sending data out.
    private OutgoingRisk OutgoingCheck(string command) =>
        CommandClassifier.IsNetwork(command) ? new OutgoingRisk(_hasReadWeb, TerminalTextIn(command)) : default;

    private static bool CanCarry(string? ignored, CommandRisk risk, PlanCheck plan, OutgoingRisk outgoing) =>
        ignored is null && risk != CommandRisk.AlwaysAsk && !plan.IsOutside && !outgoing.Asks;

    private readonly record struct OutgoingRisk(bool IsAfterWeb, string? TerminalText)
    {
        public bool Asks => IsAfterWeb || TerminalText is not null;
    }

    private static bool NeedsApproval(ApprovalMode mode, CommandRisk risk) => mode switch
    {
        ApprovalMode.IgnorePermissions => false,
        ApprovalMode.Plan => risk == CommandRisk.AlwaysAsk,
        ApprovalMode.AskForRisky => risk != CommandRisk.ReadOnly,
        _ => true,
    };

    private AgentTerminal? FindTerminal(AIFunctionArguments arguments, out string? problem)
    {
        var name = ToolArguments.GetString(arguments, "terminal")?.Trim();
        var terminals = Terminals;
        problem = null;
        if (string.IsNullOrEmpty(name) && terminals.Count == 1)
        {
            return terminals[0];
        }

        var terminal = terminals.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        if (terminal is not null)
        {
            return terminal;
        }

        var open = terminals.Count == 0 ? "no terminals are open" : "the open terminals are " + string.Join(", ", terminals.Select(item => $"\"{item.Name}\""));
        problem = string.IsNullOrEmpty(name) ? $"say which terminal: {open}." : $"there is no terminal called \"{name}\"; {open}.";
        return null;
    }

    private static string? TerminalProblem(AgentTerminal terminal)
    {
        var state = terminal.Terminal.GetState();
        if (state.IsInputHidden)
        {
            return HiddenInputNote;
        }

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

    private async Task<string> WaitForResultAsync(AgentTerminal terminal, long start, OutputSignal signal, string? command, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + _options.CommandTimeout;
        while (true)
        {
            var hasIntegration = terminal.Terminal.GetState().HasShellIntegration;
            var outcome = TryFinish(terminal, start, signal, command, hasIntegration, deadline);
            if (outcome is not null)
            {
                return outcome;
            }

            // Woken now and then even with integration, to notice a prompt for hidden input, which prints nothing more.
            var untilDeadline = deadline - DateTimeOffset.UtcNow;
            await signal.WaitAsync(hasIntegration && untilDeadline < s_quietPeriod ? untilDeadline : s_quietPeriod, cancellationToken).ConfigureAwait(false);
        }
    }

    // Without shell integration, a command counts as finished once its output has been quiet for a moment, which is only a guess.
    private string? TryFinish(AgentTerminal terminal, long start, OutputSignal signal, string? command, bool hasIntegration, DateTimeOffset deadline)
    {
        var result = terminal.Terminal.GetCommand(start);
        if (result is { IsFinished: true })
        {
            return Finish(terminal, command, result.Output, result.ExitCode, isFinished: true);
        }

        var quietFor = DateTimeOffset.UtcNow - signal.LastOutputAt;
        if (IsWaitingForSecret(terminal, quietFor))
        {
            return Finish(terminal, command, OutputOf(terminal, result), exitCode: null, isFinished: false, "Waiting: " + HiddenInputNote);
        }

        var isQuiet = !hasIntegration && quietFor >= s_quietPeriod;
        var isOver = isQuiet || signal.HasEnded;
        return isOver || DateTimeOffset.UtcNow >= deadline
            ? Finish(terminal, command, OutputOf(terminal, result), exitCode: null, isOver)
            : null;
    }

    private static string OutputOf(AgentTerminal terminal, CommandResult? result) => result?.Output ?? terminal.Terminal.GetVisibleText();

    // A prompt for a password the agent didn't expect: it stops waiting and hands over to the user, rather than time out.
    private static bool IsWaitingForSecret(AgentTerminal terminal, TimeSpan quietFor) =>
        !terminal.IsAwaitingInput && quietFor >= s_quietPeriod && terminal.Terminal.GetState().IsInputHidden;

    private const string HiddenInputNote = "a program in the terminal is waiting for hidden input, such as a password. Never type it: ask the user to "
        + "type it into the terminal, then call wait_for_command with expects_user_input set.";

    private string Finish(AgentTerminal terminal, string? command, string output, int? exitCode, bool isFinished, string? note = null)
    {
        var step = terminal.RunningPlanStep;
        if (isFinished)
        {
            terminal.RunningCommandLine = null;
            terminal.RunningPlanStep = null;
        }

        Raise(new AgentEventArgs(AgentEventKind.CommandFinished, isFinished ? "Finished" : "Still running")
        {
            Command = command,
            ExitCode = exitCode,
            Terminal = terminal.Name,
            Connection = terminal.Connection,
            Output = output,
            PlanStep = step,
            StepState = StepState(isFinished, exitCode),
        });
        SetStatus(AgentStatus.Thinking);
        var status = note ?? (isFinished, exitCode) switch
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
        var limit = OutputLimit;
        if (redacted.Length > limit)
        {
            var half = limit / 2;
            redacted = redacted[..half] + $"\n[... {redacted.Length - limit} characters left out ...]\n" + redacted[^half..];
        }

        return "<terminal_output>\n" + redacted.Replace("</terminal_output>", "</terminal_output_>", StringComparison.OrdinalIgnoreCase) + "\n</terminal_output>";
    }
}
