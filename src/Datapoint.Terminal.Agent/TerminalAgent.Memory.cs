using Datapoint.Terminal.Agent.Files;
using Datapoint.Terminal.Agent.Memory;
using Datapoint.Terminal.Agent.Tools;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Agent;

public sealed partial class TerminalAgent
{
    public AgentMemory? Memory { get; set; }

    private List<AITool> MemoryTools() =>
    [
        new ToolFunction(
            "remember",
            "Adds a short note to your memory, kept across conversations: a preference of the user's (e.g. \"use doas, not sudo\"). "
            + "The user sees each note. Facts about a machine belong in its host documentation instead, and secrets never go here.",
            """
            {"type":"object","properties":{
              "scope":{"type":"string","enum":["project","global"],"description":"project for this project only; global for everywhere."},
              "note":{"type":"string","description":"One line, under 300 characters."}},
             "required":["scope","note"]}
            """,
            RememberAsync),
        new ToolFunction(
            "forget",
            "Removes a note from your memory, given exactly as it appears there, e.g. when it's wrong or no longer applies.",
            """
            {"type":"object","properties":{
              "scope":{"type":"string","enum":["project","global"]},
              "note":{"type":"string"}},
             "required":["scope","note"]}
            """,
            ForgetAsync),
    ];

    // Notes are refused when they look like a secret (product decision 92), and every change is shown in the chat.
    private Task<string> RememberAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        if (MemoryTarget(arguments, out var scope, out var note) is not { } path)
        {
            return Task.FromResult(note);
        }

        if (_redactor.Find(note).Count > 0)
        {
            return Task.FromResult("Not remembered: the note looks like it holds a secret, and secrets never go into memory.");
        }

        var result = Change(() => MemoryFile.Add(path, note));
        if (result == WriteResult.Changed)
        {
            RaiseMemory(scope, path, "Remembered", note);
        }

        return Task.FromResult(result switch
        {
            WriteResult.Changed => "Remembered.",
            WriteResult.Unchanged => "Already remembered.",
            WriteResult.Full => "Not remembered: that memory is full. Use forget to remove notes that no longer apply, then try again.",
            _ => "Not remembered: the memory file couldn't be written.",
        });
    }

    private Task<string> ForgetAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        if (MemoryTarget(arguments, out var scope, out var note) is not { } path)
        {
            return Task.FromResult(note);
        }

        var result = Change(() => MemoryFile.Remove(path, note));
        if (result == WriteResult.Changed)
        {
            RaiseMemory(scope, path, "Forgot", note);
            return Task.FromResult("Forgotten.");
        }

        return Task.FromResult(result == WriteResult.Unchanged
            ? "Not found. Give the note exactly as it appears in memory."
            : "Not forgotten: the memory file couldn't be written.");
    }

    // The path to write, or null with the reason in note.
    private string? MemoryTarget(AIFunctionArguments arguments, out MemoryScope scope, out string note)
    {
        var scopeText = ToolArguments.GetString(arguments, "scope");
        scope = string.Equals(scopeText, "global", StringComparison.OrdinalIgnoreCase) ? MemoryScope.Global : MemoryScope.Project;
        note = string.Join(' ', (ToolArguments.GetString(arguments, "note") ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (note.Length == 0)
        {
            note = "Not changed: the note is empty.";
            return null;
        }

        if (note.Length > MemoryFile.MaxNoteLength)
        {
            note = $"Not changed: keep a note under {MemoryFile.MaxNoteLength} characters.";
            return null;
        }

        if (Memory?.PathFor(scope) is not { } path)
        {
            note = scope == MemoryScope.Project
                ? "Not changed: this window can't write the project's memory (it's open read-only)."
                : "Not changed: memory isn't available here.";
            return null;
        }

        return path;
    }

    private static WriteResult? Change(Func<WriteResult> change)
    {
        try
        {
            return change();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void RaiseMemory(MemoryScope scope, string path, string verb, string note) =>
        Raise(new AgentEventArgs(AgentEventKind.MemoryChanged, $"{verb} {(scope == MemoryScope.Global ? "everywhere" : "for this project")}: {note}") { Path = path });
}
