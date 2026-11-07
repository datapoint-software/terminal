using Datapoint.Terminal.Agent.Keys;
using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Agent.Tools;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Agent;

public sealed partial class TerminalAgent
{
    private static readonly TimeSpan s_keysSettle = TimeSpan.FromSeconds(2);

    private List<AITool> KeysTools() =>
    [
        new ToolFunction(
            "send_keys",
            "Presses keys in a full-screen program (e.g. vim, less, top) or answers a running command's prompt, then returns the "
            + "screen. Named keys go in angle brackets: <Enter>, <Esc>, <Tab>, <BS>, <Up>, <Down>, <Left>, <Right>, <PageUp>, "
            + "<PageDown>, <Home>, <End>, <F1>-<F12>, <C-c> for Ctrl+C, <M-x> for Alt+X, <lt> for \"<\". Prefer run_command with "
            + "options that avoid such programs (e.g. git --no-pager, PAGER=cat). Not for typing commands at a shell prompt.",
            """
            {"type":"object","properties":{
              "terminal":{"type":"string","description":"The name of the terminal, as listed in the user's message. May be left out when only one is open."},
              "keys":{"type":"string","description":"e.g. \"<Esc>:wq<Enter>\"."},
              "purpose":{"type":"string","description":"What the keys do, in a short sentence the user reads when approving them."}},
             "required":["keys","purpose"]}
            """,
            SendKeysAsync),
    ];

    // Keys are harder to review than a command, so they're proposed in Ask and Ask-for-risky modes alike; Auto asks only when the
    // text they type looks like a command that always needs the user.
    private async Task<string> SendKeysAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var keys = ToolArguments.GetString(arguments, "keys") ?? string.Empty;
        var purpose = ToolArguments.GetString(arguments, "purpose") ?? string.Empty;
        if (FindTerminal(arguments, out var problem) is not { } terminal)
        {
            return "Not sent: " + problem;
        }

        if (KeysProblem(terminal, keys, out var parts) is { } invalid)
        {
            return "Not sent: " + invalid;
        }

        var (approved, approvedParts, refusal) = await ApproveKeysAsync(terminal, keys, parts!, purpose, cancellationToken).ConfigureAwait(false);
        if (refusal is not null)
        {
            return refusal;
        }

        (keys, parts) = (approved, approvedParts);
        Raise(new AgentEventArgs(AgentEventKind.Notice, $"Pressed {keys} in {terminal.Name}: {purpose}") { Terminal = terminal.Name });
        SetStatus(AgentStatus.RunningCommand, terminal);
        using (var signal = new OutputSignal(terminal.Terminal))
        {
            terminal.Terminal.SendKeys(parts!);
            await SettleAsync(signal, cancellationToken).ConfigureAwait(false);
        }

        SetStatus(AgentStatus.Thinking);
        var state = terminal.Terminal.GetState();
        return (state.IsAlternateScreen ? "Sent. A full-screen program is open; the screen now shows:\n" : "Sent. The screen now shows:\n")
            + Untrusted(terminal.Terminal.GetVisibleText());
    }

    private async Task<(string Keys, IReadOnlyList<string> Parts, string? Refusal)> ApproveKeysAsync(
        AgentTerminal terminal, string keys, IReadOnlyList<string> parts, string purpose, CancellationToken cancellationToken)
    {
        var risk = TypesAlwaysAskCommand(parts) ? CommandRisk.AlwaysAsk : CommandRisk.Changes;
        if (terminal.Mode == ApprovalMode.Auto && risk != CommandRisk.AlwaysAsk)
        {
            return (keys, parts, null);
        }

        SetStatus(AgentStatus.WaitingForApproval, terminal);
        var proposal = new CommandProposal(keys, purpose, risk == CommandRisk.AlwaysAsk ? risk : CommandRisk.Changes, terminal.Name, terminal.Environment) { Kind = ProposalKind.SendKeys };
        var approval = await RequestApprovalAsync(proposal, cancellationToken).ConfigureAwait(false);
        if (!approval.Approved)
        {
            return (keys, parts, Declined(approval).Replace("Not run", "Not sent", StringComparison.Ordinal));
        }

        // The user may have edited the keys, or the screen may have changed while they decided.
        return KeysProblem(terminal, approval.Command, out var edited) is { } problem
            ? (keys, parts, "Not sent: " + problem)
            : (approval.Command, edited!, null);
    }

    // Editors and pagers run shell commands after ":!" or "!", so each typed line is checked with those taken off too.
    private static bool TypesAlwaysAskCommand(IReadOnlyList<string> parts) => KeyNotation.Typed(parts)
        .Split('\n')
        .SelectMany(line => new[] { line, line.TrimStart(' ', ':', '!') })
        .Any(line => line.Trim().Length > 0 && CommandClassifier.Classify(line.Trim()) == CommandRisk.AlwaysAsk);

    // At a shell prompt keys would type a command past the command checks, so they're only for programs that are running.
    private static string? KeysProblem(AgentTerminal terminal, string keys, out IReadOnlyList<string>? parts)
    {
        parts = KeyNotation.Parse(keys, out var problem);
        if (problem is not null)
        {
            return problem;
        }

        var state = terminal.Terminal.GetState();
        if (state.IsAlternateScreen)
        {
            return null;
        }

        if (!state.HasShellIntegration)
        {
            return "no full-screen program is open, and this shell doesn't report whether a command is running. Use run_command.";
        }

        return state.IsAtPrompt ? "the terminal is at a shell prompt. Use run_command to run commands." : null;
    }

    // Waits for the program to redraw: until output stops for a moment, or a short while at most.
    private static async Task SettleAsync(OutputSignal signal, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + s_keysSettle;
        while (DateTimeOffset.UtcNow < deadline && !signal.HasEnded)
        {
            var quietFor = DateTimeOffset.UtcNow - signal.LastOutputAt;
            if (quietFor >= TimeSpan.FromMilliseconds(300))
            {
                return;
            }

            await signal.WaitAsync(TimeSpan.FromMilliseconds(300) - quietFor, cancellationToken).ConfigureAwait(false);
        }
    }
}
