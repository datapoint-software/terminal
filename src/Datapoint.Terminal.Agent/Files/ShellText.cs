namespace Datapoint.Terminal.Agent.Files;

// Quoting that means the same in sh, bash, zsh, fish and csh, for paths without backslashes or line breaks.
public static class ShellText
{
    public static string Quote(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return "'" + text.Replace("'", @"'\''", StringComparison.Ordinal) + "'";
    }

    // A path for a command line: one under the home folder keeps an unquoted ~/ so the shell expands it.
    public static string QuotePath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return path.StartsWith("~/", StringComparison.Ordinal) ? "~/" + Quote(path[2..]) : Quote(path);
    }

    // Null when the path is one the file tools take; otherwise why not.
    public static string? PathProblem(string? path) =>
        string.IsNullOrWhiteSpace(path) ? "give the file's path."
        : !(path.StartsWith('/') || path.StartsWith("~/", StringComparison.Ordinal)) ? "give an absolute path, or one starting with ~/."
        : path.AsSpan().IndexOfAny("\\\n\r\0") >= 0 ? "the path has a backslash or a line break, which the file tools don't handle; use run_command."
        : path.EndsWith('/') ? "give a file, not a folder."
        : null;
}
