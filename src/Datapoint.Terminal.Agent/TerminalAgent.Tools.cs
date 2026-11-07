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
              "purpose":{"type":"string","description":"Why you want to run it, in a short sentence the user reads when approving it."}},
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
        if (FindTerminal(arguments, out var problem) is not { } terminal)
        {
            return "Not run: " + problem;
        }

        problem = Check(terminal, proposed);
        if (problem is not null)
        {
            return "Not run: " + problem;
        }

        var approval = await ApproveAsync(terminal, proposed!, purpose, cancellationToken).ConfigureAwait(false);
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
        return edited + await TypeAndWaitAsync(terminal, command, purpose, cancellationToken).ConfigureAwait(false);
    }

    private static string? Check(AgentTerminal terminal, string? command) => CommandCheck.Problem(command) ?? TerminalProblem(terminal);

    private static string Declined(CommandApproval approval) =>
        "Not run: the user declined." + (string.IsNullOrWhiteSpace(approval.Feedback) ? string.Empty : " They said: " + approval.Feedback);

    private async Task<string> TypeAndWaitAsync(AgentTerminal terminal, string command, string purpose, CancellationToken cancellationToken)
    {
        var start = terminal.Terminal.GetState().NextOutputLine;
        terminal.RunningCommandLine = start;
        Raise(new AgentEventArgs(AgentEventKind.CommandStarted, purpose) { Command = command, Terminal = terminal.Name });
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
        text.Append(state.HasShellIntegration ? (state.IsAtPrompt ? "At a prompt" : "A command is running") : "The shell doesn't report prompts")
            .Append(state.IsAlternateScreen ? "; a full-screen program is open" : string.Empty)
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

        var options = ToolArguments.GetStrings(arguments, "options").Where(option => !string.IsNullOrWhiteSpace(option)).Take(6).ToList();
        SetStatus(AgentStatus.WaitingForUser);
        var answer = await _user.AskAsync(new AgentQuestion(question, options), cancellationToken).ConfigureAwait(false);
        SetStatus(AgentStatus.Thinking);
        return answer is null ? "The user didn't answer." : "The user answered: " + answer;
    }

    private async Task<CommandApproval> ApproveAsync(AgentTerminal terminal, string command, string purpose, CancellationToken cancellationToken)
    {
        var risk = CommandClassifier.Classify(command);
        var needsApproval = terminal.Mode switch
        {
            ApprovalMode.Auto => risk == CommandRisk.AlwaysAsk,
            ApprovalMode.AskForRisky => risk != CommandRisk.ReadOnly,
            _ => true,
        };
        if (!needsApproval)
        {
            return CommandApproval.Approve(command);
        }

        SetStatus(AgentStatus.WaitingForApproval, terminal);
        var proposal = new CommandProposal(command, purpose, risk, terminal.Name, terminal.Environment);
        return await _user.RequestApprovalAsync(proposal, cancellationToken).ConfigureAwait(false);
    }

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

            await signal.WaitAsync(hasIntegration ? deadline - DateTimeOffset.UtcNow : s_quietPeriod, cancellationToken).ConfigureAwait(false);
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

        var isQuiet = !hasIntegration && DateTimeOffset.UtcNow - signal.LastOutputAt >= s_quietPeriod;
        var isOver = isQuiet || signal.HasEnded;
        return isOver || DateTimeOffset.UtcNow >= deadline
            ? Finish(terminal, command, result?.Output ?? terminal.Terminal.GetVisibleText(), exitCode: null, isOver)
            : null;
    }

    private string Finish(AgentTerminal terminal, string? command, string output, int? exitCode, bool isFinished)
    {
        if (isFinished)
        {
            terminal.RunningCommandLine = null;
        }

        Raise(new AgentEventArgs(AgentEventKind.CommandFinished, isFinished ? "Finished" : "Still running") { Command = command, ExitCode = exitCode, Terminal = terminal.Name, Output = output });
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
