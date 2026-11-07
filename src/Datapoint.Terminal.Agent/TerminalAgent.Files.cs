using System.Text;
using System.Text.Json;
using Datapoint.Terminal.Agent.Files;
using Datapoint.Terminal.Agent.Tools;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Agent;

// Product decisions 45 and 57: files are read exactly, beside the terminal, and changed as a diff the user approves, after a
// backup. Every read and write beside the terminal is noted in the chat (decision 58).
public sealed partial class TerminalAgent
{
    public const int MaxFileBytes = 512 * 1024;

    private static readonly UTF8Encoding s_strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private List<AITool> FileTools() =>
    [
        new ToolFunction(
            "read_file",
            "Returns a text file's exact contents from the machine a terminal is on, read beside the terminal rather than printed "
            + "in it. Use it before edit_file, and instead of cat for files you'll change.",
            """
            {"type":"object","properties":{
              "terminal":{"type":"string","description":"The name of the terminal whose machine has the file, as listed in the user's message."},
              "path":{"type":"string","description":"An absolute path, or one starting with ~/."}},
             "required":["path"]}
            """,
            ReadFileAsync),
        new ToolFunction(
            "edit_file",
            "Changes a text file on the machine a terminal is on, as a diff the user approves. A copy of the file is kept first, so the "
            + "change can be undone. Use it instead of sed, editors or echo for any change to a file. Give edits for an existing file, "
            + "each replacing text that appears exactly once; give content only to create a new file.",
            """
            {"type":"object","properties":{
              "terminal":{"type":"string","description":"The name of the terminal whose machine has the file, as listed in the user's message."},
              "path":{"type":"string","description":"An absolute path, or one starting with ~/."},
              "edits":{"type":"array","items":{"type":"object","properties":{
                "old_text":{"type":"string","description":"Text from the file, copied exactly, including enough lines to appear only once."},
                "new_text":{"type":"string","description":"The text to put in its place."}},"required":["old_text","new_text"]}},
              "content":{"type":"string","description":"The whole content of a new file."},
              "purpose":{"type":"string","description":"Why, in a short sentence the user reads when approving it."},
              "plan_step":{"type":"integer","description":"The number of the approved plan's step this carries out, if any."}},
             "required":["path","purpose"]}
            """,
            EditFileAsync),
    ];

    private async Task<string> ReadFileAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        if (FileTarget(arguments, out var terminal, out var path) is { } refusal)
        {
            return "Not read: " + refusal;
        }

        var (file, problem) = await TryReadFileAsync(terminal!, path!, cancellationToken).ConfigureAwait(false);
        if (file is null)
        {
            return "Not read: " + problem;
        }

        if (!file.Exists)
        {
            return $"There's no file at {path}.";
        }

        return Decode(file.Content) is { } text
            ? (file.IsTruncated ? $"[Only the first {MaxFileBytes / 1024} KB.]\n" : string.Empty) + Untrusted(text)
            : "Not shown: the file isn't text (it isn't UTF-8, or it has NUL bytes).";
    }

    // Reads beside the terminal and says so in the chat.
    private async Task<(HostFile? File, string? Problem)> TryReadFileAsync(AgentTerminal terminal, string path, CancellationToken cancellationToken)
    {
        Raise(new AgentEventArgs(AgentEventKind.Notice, $"Read {path} on {terminal.Name}'s machine, beside the terminal.") { Terminal = terminal.Name });
        try
        {
            return (await terminal.Files!.ReadAsync(path, MaxFileBytes, cancellationToken).ConfigureAwait(false), null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException
            or Sessions.Ssh.Channels.SshChannelException or Sessions.Ssh.SshDisconnectedException)
        {
            return (null, exception.Message);
        }
    }

    // The terminal and path a file tool works on, or why it can't.
    private string? FileTarget(AIFunctionArguments arguments, out AgentTerminal? terminal, out string? path)
    {
        path = ToolArguments.GetString(arguments, "path")?.Trim();
        terminal = FindTerminal(arguments, out var problem);
        if (terminal is null)
        {
            return problem;
        }

        if (terminal.Files is null)
        {
            return "file access beside the terminal isn't available for this terminal yet; use run_command.";
        }

        // File access reaches the tab's own machine, not the one a session inside it is logged in to.
        if (terminal.Terminal.GetState().Session is { } session)
        {
            return $"the terminal is in a {session} started inside it, and file access would reach this tab's own machine instead. Use run_command to read the file, and ask the user how to change it, or suggest opening the host as a connection.";
        }

        if (ShellText.PathProblem(path) is { } pathProblem)
        {
            return pathProblem;
        }

        return IgnoredFileCheck?.Invoke(terminal, "cat " + ShellText.QuotePath(path!)) is null
            ? null
            : "the file is listed in .terminalignore. Ask the user to handle it.";
    }

    private static string? Decode(byte[] content)
    {
        if (Array.IndexOf(content, (byte)0) >= 0)
        {
            return null;
        }

        try
        {
            return s_strictUtf8.GetString(content);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    private static List<FileEdit> ReadEdits(AIFunctionArguments arguments)
    {
        if (!arguments.TryGetValue("edits", out var value) || value is not JsonElement { ValueKind: JsonValueKind.Array } array)
        {
            return [];
        }

        return [.. array.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Select(item => new FileEdit(Text(item, "old_text"), Text(item, "new_text")))];
    }

    private static string Text(JsonElement item, string name) =>
        item.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() ?? string.Empty : string.Empty;
}
