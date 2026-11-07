using System.Globalization;
using Datapoint.Terminal.Agent.Tools;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Agent;

// Product decision 46, watch and alert: the agent waits on a command that keeps printing until something appears, then reports.
// Plain text, not patterns, so nothing the model writes can make the matching slow.
public sealed partial class TerminalAgent
{
    public const int MaxWatchMinutes = 240;

    private const int MaxWatchTexts = 10;
    private const int WatchContextLines = 20;

    private List<AITool> WatchTools() =>
    [
        new ToolFunction(
            "watch_terminal",
            "Waits while a command already running in a terminal keeps printing (e.g. tail -f, journalctl -f, kubectl rollout "
            + "status), until a line contains one of the given texts, the command finishes, or the time runs out, then returns the "
            + "lines around what happened. Start the command with run_command first. The user can stop the wait by typing.",
            """
            {"type":"object","properties":{
              "terminal":{"type":"string","description":"The name of the terminal, as listed in the user's message."},
              "until":{"type":"array","items":{"type":"string"},"description":"Texts to watch for, matched ignoring case, e.g. [\"successfully rolled out\", \"error\"]."},
              "minutes":{"type":"integer","description":"How long to wait at most, up to 240. Default 30."}},
             "required":["until"]}
            """,
            WatchTerminalAsync),
    ];

    private async Task<string> WatchTerminalAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        if (FindTerminal(arguments, out var problem) is not { } terminal)
        {
            return "Not watched: " + problem;
        }

        var texts = ToolArguments.GetStrings(arguments, "until").Select(text => text.Trim()).Where(text => text.Length is > 0 and <= 200).Take(MaxWatchTexts).ToList();
        if (texts.Count == 0)
        {
            return "Not watched: give at least one text to watch for.";
        }

        var minutes = int.TryParse(ToolArguments.GetString(arguments, "minutes"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var given) ? Math.Clamp(given, 1, MaxWatchMinutes) : 30;
        Raise(new AgentEventArgs(AgentEventKind.Notice, $"Watching {terminal.Name} for {string.Join(" or ", texts.Select(text => $"\"{text}\""))}, for up to {minutes} minutes.") { Terminal = terminal.Name });
        SetStatus(AgentStatus.RunningCommand, terminal);
        try
        {
            return await WatchAsync(terminal, texts, TimeSpan.FromMinutes(minutes), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            SetStatus(AgentStatus.Thinking);
        }
    }

    private async Task<string> WatchAsync(AgentTerminal terminal, List<string> texts, TimeSpan limit, CancellationToken cancellationToken)
    {
        using var signal = new OutputSignal(terminal.Terminal);
        var from = terminal.Terminal.GetState().NextOutputLine;
        void Move(object? sender, Emulator.LinesMovedEventArgs e) => Interlocked.Exchange(ref from, e.Map.Map(Interlocked.Read(ref from)));
        terminal.Terminal.LinesMoved += Move;
        try
        {
            return await WatchLinesAsync(terminal, signal, texts, limit, () => Interlocked.Read(ref from), next => Interlocked.Exchange(ref from, next), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            terminal.Terminal.LinesMoved -= Move;
        }
    }

    private async Task<string> WatchLinesAsync(AgentTerminal terminal, OutputSignal signal, List<string> texts, TimeSpan limit, Func<long> getFrom, Action<long> setFrom, CancellationToken cancellationToken)
    {
        var started = Clock.GetUtcNow();
        var seen = new List<string>();
        while (true)
        {
            var from = getFrom();
            var lines = terminal.Terminal.GetTextSince(from).Split('\n');
            var match = Array.FindIndex(lines, line => texts.Any(text => line.Contains(text, StringComparison.OrdinalIgnoreCase)));
            if (match >= 0)
            {
                return WatchResult($"Seen after {Format(Clock.GetUtcNow() - started)}", seen, lines[..(match + 1)]);
            }

            if (WatchStop(terminal, signal, started, limit) is { } stop)
            {
                return WatchResult(stop, seen, lines);
            }

            // Lines already checked aren't read again; the last one may still be growing.
            Remember(seen, lines[..^1]);
            setFrom(Math.Max(from, terminal.Terminal.GetState().NextOutputLine - 1));
            await signal.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
        }
    }

    private string? WatchStop(AgentTerminal terminal, OutputSignal signal, DateTimeOffset started, TimeSpan limit)
    {
        var state = terminal.Terminal.GetState();
        var elapsed = Clock.GetUtcNow() - started;
        return signal.HasEnded ? "The terminal closed before anything matched"
            : state.HasShellIntegration && state.IsAtPrompt ? $"The command finished after {Format(elapsed)} without anything matching"
            : elapsed >= limit ? $"Nothing matched in {Format(elapsed)}; the command is still running"
            : null;
    }

    private static void Remember(List<string> seen, IEnumerable<string> lines)
    {
        seen.AddRange(lines);
        seen.RemoveRange(0, Math.Max(0, seen.Count - WatchContextLines));
    }

    private string WatchResult(string what, List<string> seen, string[] lines)
    {
        var context = seen.Concat(lines).TakeLast(WatchContextLines);
        return what + ". The last lines:\n" + Untrusted(string.Join('\n', context).TrimEnd());
    }

    private static string Format(TimeSpan elapsed) =>
        elapsed.TotalMinutes >= 1
            ? ((int)elapsed.TotalMinutes).ToString(CultureInfo.InvariantCulture) + " min"
            : ((int)elapsed.TotalSeconds).ToString(CultureInfo.InvariantCulture) + " s";
}
